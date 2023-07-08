using StackExchange.Redis;
using System.Net;

namespace MessagePlatform.Gateway.Middleware;

// custom rate limiting using Redis - very common pattern
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next, 
        IConnectionMultiplexer redis,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"rate_limit:{clientIp}";
        
        // get current request count
        var currentCount = await _redis.StringGetAsync(key);
        var requests = currentCount.HasValue ? (int)currentCount : 0;
        
        // rate limit: 100 requests per minute
        if (requests >= 100)
        {
            _logger.LogWarning("Rate limit exceeded for IP {ClientIp}", clientIp);
            
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
            return;
        }
        
        // increment counter with expiry
        await _redis.StringIncrementAsync(key);
        await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        
        // add rate limit headers
        context.Response.Headers.Add("X-RateLimit-Limit", "100");
        context.Response.Headers.Add("X-RateLimit-Remaining", (99 - requests).ToString());
        
        await _next(context);
    }
}