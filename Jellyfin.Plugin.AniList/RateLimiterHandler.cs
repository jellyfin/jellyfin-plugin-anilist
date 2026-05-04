using System;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Jellyfin.Plugin.AniList;

internal class RateLimiterHandler : DelegatingHandler
{
    static RateLimiter _rateLimiter;

    static readonly RateLimiter _burstLimiter = new SlidingWindowRateLimiter(
        new SlidingWindowRateLimiterOptions()
        {
            AutoReplenishment = true,
            PermitLimit = 4,
            QueueLimit = 4096,
            Window = TimeSpan.FromSeconds(1),
            SegmentsPerWindow = 2,
        }
    );

    public RateLimiterHandler(int rateLimit)
    {
        if (_rateLimiter != null)
        {
            return;
        }

        lock (_burstLimiter)
        {
            if (_rateLimiter != null)
            {
                return;
            }

            _rateLimiter = new FixedWindowRateLimiter(
                new FixedWindowRateLimiterOptions()
                {
                    AutoReplenishment = true,
                    PermitLimit = Plugin.Instance.Configuration.ApiRateLimit,
                    QueueLimit = 4096,
                    Window = TimeSpan.FromMinutes(1),
                }
            );
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using RateLimitLease rateLimiterLease = await _rateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        using RateLimitLease burstLimiterLease = await _burstLimiter.AcquireAsync(cancellationToken: cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
