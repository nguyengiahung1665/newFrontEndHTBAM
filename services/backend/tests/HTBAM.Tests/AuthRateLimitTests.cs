using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using HTBAM.Api.Support;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HTBAM.Tests;

public sealed class AuthRateLimitTests
{
    [Fact]
    public async Task Requests_within_limit_are_allowed_and_excess_is_rejected()
    {
        using var limiter = CreateLimiter(2, 60);
        var client = Context("10.0.0.10");

        using var first = await limiter.AcquireAsync(client);
        using var second = await limiter.AcquireAsync(client);
        using var rejected = await limiter.AcquireAsync(client);

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public async Task Limited_partition_does_not_block_another_ip()
    {
        using var limiter = CreateLimiter(1, 60);
        var limitedClient = Context("10.0.0.10");

        using var accepted = await limiter.AcquireAsync(limitedClient);
        using var rejected = await limiter.AcquireAsync(limitedClient);
        using var otherClient =
            await limiter.AcquireAsync(Context("10.0.0.11"));

        Assert.True(accepted.IsAcquired);
        Assert.False(rejected.IsAcquired);
        Assert.True(otherClient.IsAcquired);
    }

    [Fact]
    public async Task Rejection_has_status_retry_after_and_friendly_json()
    {
        using var limiter = CreateLimiter(1, 60);
        var client = Context("10.0.0.10");
        using var accepted = await limiter.AcquireAsync(client);
        using var rejected = await limiter.AcquireAsync(client);
        var responseContext = new DefaultHttpContext();
        responseContext.Response.Body = new MemoryStream();

        await AuthRateLimitPolicy.WriteRejectedAsync(
            responseContext,
            rejected,
            60,
            CancellationToken.None);

        responseContext.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(
            responseContext.Response.Body);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            responseContext.Response.StatusCode);
        Assert.True(
            int.Parse(responseContext.Response.Headers.RetryAfter!) > 0);
        Assert.Equal(
            "LOGIN_RATE_LIMITED",
            json.RootElement.GetProperty("code").GetString());
        Assert.Contains(
            "Vui lòng thử lại sau",
            json.RootElement.GetProperty("message").GetString());
        Assert.True(
            json.RootElement
                .GetProperty("retryAfterSeconds")
                .GetInt32() > 0);
    }

    [Fact]
    public async Task Client_can_try_again_after_window_expires()
    {
        using var limiter = CreateLimiter(1, 1);
        var client = Context("10.0.0.10");
        using var accepted = await limiter.AcquireAsync(client);
        using var rejected = await limiter.AcquireAsync(client);

        Assert.True(accepted.IsAcquired);
        Assert.False(rejected.IsAcquired);

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        using var afterWindow = await limiter.AcquireAsync(client);
        Assert.True(afterWindow.IsAcquired);
    }

    private static PartitionedRateLimiter<HttpContext> CreateLimiter(
        int permitLimit,
        int windowSeconds)
    {
        var settings = new AuthRateLimitSettings
        {
            PermitLimit = permitLimit,
            WindowSeconds = windowSeconds,
        };

        return PartitionedRateLimiter.Create<HttpContext, string>(
            context =>
                AuthRateLimitPolicy.Partition(
                    context,
                    settings));
    }

    private static DefaultHttpContext Context(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
