using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers.AniList;

/// <summary>
/// Spaces out requests to the AniList API so that a library scan stays inside the
/// documented rate limit (https://docs.anilist.co/guide/rate-limiting).
///
/// The configured requests-per-minute is only an upper bound: AniList reports the allowance
/// it is applying through X-RateLimit-Limit and how much of it is left through
/// X-RateLimit-Remaining, and the limiter follows both. The remaining count is what keeps the
/// plugin polite when it is not the only thing spending the allowance - the limit is per IP,
/// so a second Jellyfin server, another plugin or just the website in a browser eats into the
/// same budget. Rather than racing ahead on its own schedule and rediscovering the ceiling
/// through HTTP 429s, the limiter spreads whatever is left over the rest of the window, so a
/// shared allowance slows the scan down instead of exhausting it.
/// </summary>
internal static class AniListRateLimiter
{
    /// <summary>
    /// The allowance assumed before AniList has reported one of its own. The documented limit
    /// is 90/min, but it is degraded to 30/min server side.
    /// </summary>
    internal const int DefaultRequestsPerMinute = 30;

    /// <summary>
    /// The length of the allowance window AniList applies. Successful responses report how
    /// much of the allowance is left but not when it resets, so the documented one minute is
    /// assumed and tracked against the counts as they come in.
    /// </summary>
    private static readonly TimeSpan _rateLimitWindow = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Task.Delay may fire fractionally early, so a spin of the wait loop never sleeps
    /// for less than this.
    /// </summary>
    private static readonly TimeSpan _minimumDelay = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// How long to hold off for when AniList rate limits without saying for how long. The
    /// documented penalty for exceeding the limit is a one minute timeout.
    /// </summary>
    private static readonly TimeSpan _fallbackRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Serializes callers so that they queue behind each other rather than all sleeping
    /// in parallel and then firing at the same moment.
    /// </summary>
    private static readonly SemaphoreSlim _requestGate = new(1, 1);

    /// <summary>
    /// Guards the allowance AniList has reported: the limit, what is left of it and when the
    /// window it belongs to is believed to have started. Responses are read outside the
    /// request gate, so several can land at once.
    /// </summary>
    private static readonly object _reportedStateLock = new();

    /// <summary>
    /// When the next request may be sent, on the monotonic clock. Guarded by
    /// <see cref="_requestGate"/> for writes made while holding a slot; a rate limit
    /// response pushes it back from outside the gate through <see cref="DelayNextRequest"/>.
    /// </summary>
    private static long _nextRequestTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// The allowance AniList last reported through X-RateLimit-Limit, or 0 before any
    /// response has carried one.
    /// </summary>
    private static int _reportedRequestsPerMinute;

    /// <summary>
    /// What AniList last reported to be left of the allowance through X-RateLimit-Remaining,
    /// or -1 before any response has carried one.
    /// </summary>
    private static int _reportedRemaining = -1;

    /// <summary>
    /// When the window <see cref="_reportedRemaining"/> belongs to is believed to have
    /// started, on the monotonic clock. AniList does not date its windows, so this is taken
    /// from the point the remaining count was last seen to go up, which can only happen when
    /// a window rolls over.
    /// </summary>
    private static long _windowStartTimestamp = Stopwatch.GetTimestamp();

    /// <summary>
    /// Waits until the rate limit allows another request to be sent.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal static async Task WaitForRequestSlot(ILogger logger, CancellationToken cancellationToken)
    {
        // The gate is held across the wait so that concurrent callers queue up.
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var waited = false;
            for (var remaining = GetRemainingInterval(); remaining > TimeSpan.Zero; remaining = GetRemainingInterval())
            {
                if (!waited)
                {
                    logger.LogDebug("Waiting {Delay} ms for the AniList rate limit.", remaining.TotalMilliseconds);
                    waited = true;
                }

                await Task.Delay(remaining < _minimumDelay ? _minimumDelay : remaining, cancellationToken).ConfigureAwait(false);
            }

            _nextRequestTimestamp = Stopwatch.GetTimestamp() + GetRequestIntervalTicks();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>
    /// Takes note of the allowance AniList reported on a successful response, so that the
    /// spacing follows what is actually left rather than the configured allowance.
    /// </summary>
    /// <param name="response">The response to read the X-RateLimit-* headers from.</param>
    internal static void RegisterResponse(HttpResponseMessage response, ILogger logger)
    {
        RegisterReportedLimit(response, logger);

        var remaining = ReadIntHeader(response, "X-RateLimit-Remaining");
        if (remaining < 0)
        {
            // Nothing reported, so the configured spacing is all there is to go on.
            return;
        }

        RegisterReportedRemaining(remaining);

        if (remaining > 0)
        {
            return;
        }

        // The allowance is spent; nothing may be sent until the window resets, whatever the
        // configured spacing says.
        var untilReset = GetTimeUntilReset(response);
        if (untilReset > TimeSpan.Zero)
        {
            logger.LogInformation("AniList rate limit exhausted. Holding requests for {Delay} ms.", untilReset.TotalMilliseconds);
            DelayNextRequest(untilReset);
        }
    }

    /// <summary>
    /// Holds every caller back after AniList answered with HTTP 429.
    /// </summary>
    /// <param name="response">The rate limited response.</param>
    /// <returns>How long the caller has to wait before retrying.</returns>
    internal static TimeSpan RegisterRateLimited(HttpResponseMessage response, ILogger logger)
    {
        RegisterReportedLimit(response, logger);

        // Whatever the headers say, an HTTP 429 means the allowance is gone.
        RegisterReportedRemaining(0);

        var delay = response.Headers.RetryAfter?.Delta;
        if (delay is null && response.Headers.RetryAfter?.Date is { } retryAfterDate)
        {
            delay = retryAfterDate - DateTimeOffset.UtcNow;
        }

        if (delay is null || delay <= TimeSpan.Zero)
        {
            var reset = ReadIntHeader(response, "X-RateLimit-Reset");
            if (reset > 0)
            {
                delay = DateTimeOffset.FromUnixTimeSeconds(reset) - DateTimeOffset.UtcNow;
            }
        }

        var retryDelay = delay is { } value && value > TimeSpan.Zero ? value : _fallbackRetryDelay;

        // Everything queued behind this caller has to wait it out too, otherwise they
        // each spend a request rediscovering the same 429.
        DelayNextRequest(retryDelay);

        return retryDelay;
    }

    /// <summary>
    /// Takes note of the allowance reported through X-RateLimit-Limit.
    /// </summary>
    private static void RegisterReportedLimit(HttpResponseMessage response, ILogger logger)
    {
        var limit = ReadIntHeader(response, "X-RateLimit-Limit");
        if (limit <= 0)
        {
            return;
        }

        lock (_reportedStateLock)
        {
            if (limit == _reportedRequestsPerMinute)
            {
                return;
            }

            _reportedRequestsPerMinute = limit;
        }

        logger.LogDebug("AniList reports a rate limit of {Limit} requests per minute.", limit);
    }

    /// <summary>
    /// Takes note of what AniList reported to be left of the allowance, and of the window
    /// rolling over when the count goes back up.
    /// </summary>
    private static void RegisterReportedRemaining(int remaining)
    {
        lock (_reportedStateLock)
        {
            // A count can only go up when the allowance was replenished, so that dates the
            // start of the window it belongs to. The assumed window running out re-dates it
            // too, in case a reset went unnoticed - the counts only arrive one request at a
            // time, and a request that failed outright carries none at all.
            if (remaining > _reportedRemaining || Stopwatch.GetElapsedTime(_windowStartTimestamp) >= _rateLimitWindow)
            {
                _windowStartTimestamp = Stopwatch.GetTimestamp();
            }

            _reportedRemaining = remaining;
        }
    }

    /// <summary>
    /// How long until the allowance is expected to be replenished.
    /// </summary>
    private static TimeSpan GetTimeUntilReset(HttpResponseMessage response)
    {
        var reset = ReadIntHeader(response, "X-RateLimit-Reset");
        if (reset > 0)
        {
            var untilReset = DateTimeOffset.FromUnixTimeSeconds(reset) - DateTimeOffset.UtcNow;
            if (untilReset > TimeSpan.Zero)
            {
                return untilReset;
            }
        }

        // Only rate limited responses carry a reset timestamp, so otherwise fall back to the
        // end of the window the remaining counts have been tracked against.
        var untilWindowEnd = GetTimeUntilWindowEnd();

        return untilWindowEnd > TimeSpan.Zero ? untilWindowEnd : _fallbackRetryDelay;
    }

    /// <summary>
    /// Pushes the next request slot at least <paramref name="delay"/> into the future.
    /// </summary>
    private static void DelayNextRequest(TimeSpan delay)
    {
        var target = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * delay.TotalSeconds);

        // Never pull the slot forward: another response may have asked for longer.
        var current = Interlocked.Read(ref _nextRequestTimestamp);
        while (target > current)
        {
            var previous = Interlocked.CompareExchange(ref _nextRequestTimestamp, target, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static TimeSpan GetRemainingInterval()
        => Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), Interlocked.Read(ref _nextRequestTimestamp));

    private static TimeSpan GetTimeUntilWindowEnd()
    {
        lock (_reportedStateLock)
        {
            return _rateLimitWindow - Stopwatch.GetElapsedTime(_windowStartTimestamp);
        }
    }

    /// <summary>
    /// The spacing to leave before the next request, in monotonic clock ticks: never less than
    /// the configured allowance asks for, and stretched further when what AniList reports to be
    /// left of its allowance is running out faster than that.
    /// </summary>
    private static long GetRequestIntervalTicks()
    {
        var configuredInterval = GetConfiguredIntervalTicks();

        int remaining;
        TimeSpan untilWindowEnd;
        lock (_reportedStateLock)
        {
            remaining = _reportedRemaining;
            untilWindowEnd = _rateLimitWindow - Stopwatch.GetElapsedTime(_windowStartTimestamp);
        }

        if (remaining <= 0 || untilWindowEnd <= TimeSpan.Zero)
        {
            return configuredInterval;
        }

        var spreadInterval = (long)(Stopwatch.Frequency * (untilWindowEnd.TotalSeconds / remaining));

        return Math.Max(configuredInterval, spreadInterval);
    }

    /// <summary>
    /// The spacing between requests the allowance asks for, in monotonic clock ticks. The
    /// configured allowance is capped by whatever AniList last reported, and a configured
    /// allowance of zero or less leaves the spacing to the reported one alone.
    /// </summary>
    private static long GetConfiguredIntervalTicks()
    {
        var configured = Plugin.Instance?.Configuration.AniDbRateLimit ?? DefaultRequestsPerMinute;

        int reported;
        lock (_reportedStateLock)
        {
            reported = _reportedRequestsPerMinute;
        }

        int requestsPerMinute;
        if (configured <= 0)
        {
            requestsPerMinute = reported > 0 ? reported : 0;
        }
        else if (reported > 0)
        {
            requestsPerMinute = Math.Min(configured, reported);
        }
        else
        {
            requestsPerMinute = configured;
        }

        if (requestsPerMinute <= 0)
        {
            return 0;
        }

        return (long)(Stopwatch.Frequency * (60d / requestsPerMinute));
    }

    private static int ReadIntHeader(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values))
        {
            return -1;
        }

        var value = values.FirstOrDefault();

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }
}
