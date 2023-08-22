using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace MessagePlatform.Infrastructure.Resilience;

public static class PollyStartupExtensions
{
    public static IServiceCollection AddPollyResilienceServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // register resilience services
        services.AddResilienceServices();
        services.AddScoped<ResilientDatabaseService>();

        // configure named HTTP clients with different policies
        ConfigureUserServiceClient(services);
        ConfigureNotificationServiceClient(services);
        ConfigureAnalyticsServiceClient(services);

        return services;
    }

    private static void ConfigureUserServiceClient(IServiceCollection services)
    {
        services.AddHttpClient("UserService", client =>
        {
            client.BaseAddress = new Uri("https://user-service.company.com");
            client.DefaultRequestHeaders.Add("User-Agent", "MessagePlatform/1.0");
        })
        .AddPolicyHandler(GetAggressiveRetryPolicy())
        .AddPolicyHandler(GetFastCircuitBreakerPolicy());
    }

    private static void ConfigureNotificationServiceClient(IServiceCollection services)
    {
        services.AddHttpClient("NotificationService", client =>
        {
            client.BaseAddress = new Uri("https://notifications.company.com");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddPolicyHandler(GetSimpleRetryPolicy())
        .AddPolicyHandler(GetLenientCircuitBreakerPolicy());
    }

    private static void ConfigureAnalyticsServiceClient(IServiceCollection services)
    {
        // analytics is not critical, so use fallback instead of circuit breaker
        services.AddHttpClient("AnalyticsService", client =>
        {
            client.BaseAddress = new Uri("https://analytics.company.com");
        })
        .AddPolicyHandler(GetFallbackPolicy());
    }

    private static IAsyncPolicy<HttpResponseMessage> GetAggressiveRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 1000),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"UserService retry {retryCount} in {timespan.TotalSeconds}s");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetSimpleRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .RetryAsync(2);
    }

    private static IAsyncPolicy<HttpResponseMessage> GetFastCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetLenientCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .FallbackAsync(
                fallbackAction: async cancellationToken =>
                {
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    response.Content = new StringContent("{ \"source\": \"fallback\", \"data\": [] }");
                    return response;
                },
                onFallback: result =>
                {
                    Console.WriteLine("Analytics service fallback triggered");
                    return Task.CompletedTask;
                });
    }
}

// configuration options for Polly
public class PollyOptions
{
    public RetryOptions Retry { get; set; } = new();
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    public TimeoutOptions Timeout { get; set; } = new();
}

public class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int BaseDelayMs { get; set; } = 1000;
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public int DurationOfBreakSeconds { get; set; } = 30;
}

public class TimeoutOptions
{
    public int TimeoutSeconds { get; set; } = 10;
}