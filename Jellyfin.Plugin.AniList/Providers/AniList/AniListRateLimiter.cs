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
/// The configured requests-per-minute is only the starting point: AniList reports the
/// allowance it is actually applying on every response through X-RateLimit-Limit and
/// X-RateLimit-Remaining, and the limiter tightens itself to whatever it is told. That
/// matters right now because the documented 90/min is degraded to 30/min server side.
/// </summary>
internal static class AniListRateLimiter
{
    /// <summary>
    /// The allowance assumed before AniList has reported one of its own.
    /// </summary>
    internal const int DefaultRequestsPerMinute = 30;

    /// <summary>
    /// Task.Delay may fire fractionally early, so a spin of the wait loop never sleeps
    /// for less than this.
    /// </summary>
    private static readonly TimeSpan _minimumDelay = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// How long to hold off for when AniList rate limits without saying for how long.
    /// </summary>
    private static readonly TimeSpan _fallbackRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Serializes callers so that they queue behind each other rather than all sleeping
    /// in parallel and then firing at the same moment.
    /// </summary>
    private static readonly SemaphoreSlim _requestGate = new(1, 1);

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
    /// Takes note of the rate limit AniList reported on a successful response, so that the
    /// spacing follows the allowance actually being applied rather than the configured one.
    /// </summary>
    /// <param name="response">The response to read the X-RateLimit-* headers from.</param>
    internal static void RegisterResponse(HttpResponseMessage response, ILogger logger)
    {
        var limit = ReadIntHeader(response, "X-RateLimit-Limit");
        if (limit > 0 && limit != _reportedRequestsPerMinute)
        {
            logger.LogDebug("AniList reports a rate limit of {Limit} requests per minute.", limit);
            _reportedRequestsPerMinute = limit;
        }

        // A remaining count of zero means the allowance is spent; nothing may be sent
        // until the window resets, whatever the configured spacing says.
        var remaining = ReadIntHeader(response, "X-RateLimit-Remaining");
        if (remaining == 0)
        {
            var reset = ReadIntHeader(response, "X-RateLimit-Reset");
            var untilReset = reset > 0
                ? DateTimeOffset.FromUnixTimeSeconds(reset) - DateTimeOffset.UtcNow
                : _fallbackRetryDelay;

            if (untilReset > TimeSpan.Zero)
            {
                logger.LogInformation("AniList rate limit exhausted. Holding requests for {Delay} ms.", untilReset.TotalMilliseconds);
                DelayNextRequest(untilReset);
            }
        }
    }

    /// <summary>
    /// Holds every caller back after AniList answered with HTTP 429.
    /// </summary>
    /// <param name="response">The rate limited response.</param>
    /// <returns>How long the caller has to wait before retrying.</returns>
    internal static TimeSpan RegisterRateLimited(HttpResponseMessage response)
    {
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

    /// <summary>
    /// The spacing between requests, in monotonic clock ticks. The configured allowance is
    /// capped by whatever AniList last reported, and a configured allowance of zero or less
    /// leaves the spacing to the reported one alone.
    /// </summary>
    private static long GetRequestIntervalTicks()
    {
        var configured = Plugin.Instance?.Configuration.AniDbRateLimit ?? DefaultRequestsPerMinute;
        var reported = _reportedRequestsPerMinute;

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
