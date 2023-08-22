using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace MessagePlatform.Infrastructure.Resilience;

public static class ResilienceConfiguration
{
    public static IServiceCollection AddResilienceServices(this IServiceCollection services)
    {
        // configure different HTTP clients with different resilience policies
        
        // critical service - aggressive retries and circuit breaker
        services.AddHttpClient<ICriticalService, CriticalService>("CriticalService", client =>
        {
            client.BaseAddress = new Uri("https://critical-api.company.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        // external service - more lenient policies
        services.AddHttpClient<IExternalApiService, ExternalApiService>("ExternalApi", client =>
        {
            client.BaseAddress = new Uri("https://external-api.thirdparty.com");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddPolicyHandler(GetExternalRetryPolicy())
        .AddPolicyHandler(GetExternalCircuitBreakerPolicy());

        // internal service - simple retry only
        services.AddHttpClient<IInternalService, InternalService>("InternalService", client =>
        {
            client.BaseAddress = new Uri("https://internal-api.company.com");
        })
        .AddPolicyHandler(GetSimpleRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount} for {context.OperationKey} in {timespan}");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    Console.WriteLine($"Circuit opened for {duration}");
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit closed");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(10);
    }

    // more lenient for external services
    private static IAsyncPolicy<HttpResponseMessage> GetExternalRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetExternalCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(2));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetSimpleRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .RetryAsync(3);
    }
}

// service interfaces for the example
public interface ICriticalService
{
    Task<string> GetCriticalDataAsync();
}

public interface IInternalService  
{
    Task<string> GetInternalDataAsync();
}

public class CriticalService : ICriticalService
{
    private readonly HttpClient _httpClient;

    public CriticalService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetCriticalDataAsync()
    {
        var response = await _httpClient.GetAsync("/api/critical-data");
        return await response.Content.ReadAsStringAsync();
    }
}

public class InternalService : IInternalService
{
    private readonly HttpClient _httpClient;

    public InternalService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetInternalDataAsync()
    {
        var response = await _httpClient.GetAsync("/api/internal-data");
        return await response.Content.ReadAsStringAsync();
    }
}