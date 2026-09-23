using System.Globalization;
using System.Threading.RateLimiting;

namespace HTBAM.Api.Support;

public sealed class AuthRateLimitSettings
{
    public int PermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}

public static class AuthRateLimitPolicy
{
    public static RateLimitPartition<string> Partition(
        HttpContext context,
        AuthRateLimitSettings settings)
    {
        var partitionKey =
            context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown-client";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, settings.PermitLimit),
                Window = TimeSpan.FromSeconds(
                    Math.Max(1, settings.WindowSeconds)),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    public static int RetryAfterSeconds(
        RateLimitLease lease,
        int fallbackSeconds)
    {
        if (
            lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            return Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        return Math.Max(1, fallbackSeconds);
    }

    public static async ValueTask WriteRejectedAsync(
        HttpContext context,
        RateLimitLease lease,
        int fallbackSeconds,
        CancellationToken cancellationToken)
    {
        var retryAfterSeconds =
            RetryAfterSeconds(lease, fallbackSeconds);

        context.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        await context.Response.WriteAsJsonAsync(
            new
            {
                code = "LOGIN_RATE_LIMITED",
                message = $"Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau {retryAfterSeconds} giây.",
                retryAfterSeconds,
            },
            cancellationToken);
    }
}
