using System;
using System.Net.Http;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

internal class RateLimiterHandler : DelegatingHandler
{
    FixedWindowRateLimiter rateLimiter;

    public RateLimiterHandler(int rateLimit)
    {
        this.rateLimiter = new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions()
            {
                AutoReplenishment = true,
                PermitLimit = rateLimit,
                QueueLimit = 24,
                Window = TimeSpan.FromMinutes(1),
            }
        );
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using RateLimitLease lease = await this.rateLimiter.AcquireAsync(cancellationToken: cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (this.rateLimiter != null)
            {
                this.rateLimiter.Dispose();
            }
        }
    }
}
