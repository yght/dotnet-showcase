using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MessagePlatform.Infrastructure.Resilience;

// this shows how to properly use circuit breakers in real apps
public interface IExternalApiService
{
    Task<string> GetUserProfileAsync(string userId);
    Task<bool> SendNotificationAsync(string userId, string message);
    Task<List<string>> GetRecommendationsAsync(string userId);
}

public class ExternalApiService : IExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiService> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly IAsyncPolicy<HttpResponseMessage> _combinedPolicy;

    public ExternalApiService(HttpClient httpClient, ILogger<ExternalApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // retry policy - try 3 times with exponential backoff
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}s for {Operation}",
                        retryCount, timespan.TotalSeconds, context.OperationKey);
                });

        // circuit breaker - open circuit after 3 consecutive failures
        _circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (result, duration) =>
                {
                    _logger.LogError("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset - service appears healthy");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open - testing service");
                });

        // combine retry + circuit breaker
        _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
    }

    public async Task<string> GetUserProfileAsync(string userId)
    {
        try
        {
            var context = new Context($"GetUserProfile-{userId}");
            
            var response = await _combinedPolicy.ExecuteAsync(async (ctx) =>
            {
                _logger.LogDebug("Fetching user profile for {UserId}", userId);
                return await _httpClient.GetAsync($"/api/users/{userId}");
            }, context);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            
            _logger.LogWarning("Failed to get user profile for {UserId}: {StatusCode}", 
                userId, response.StatusCode);
            return "{}"; // fallback
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogError("Circuit breaker is open - returning cached/default profile for {UserId}", userId);
            return GetCachedUserProfile(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting user profile for {UserId}", userId);
            return "{}";
        }
    }

    public async Task<bool> SendNotificationAsync(string userId, string message)
    {
        try
        {
            var context = new Context($"SendNotification-{userId}");
            
            var response = await _combinedPolicy.ExecuteAsync(async (ctx) =>
            {
                var payload = new { UserId = userId, Message = message };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                return await _httpClient.PostAsync("/api/notifications", content);
            }, context);

            return response.IsSuccessStatusCode;
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogError("Circuit breaker open - notification queued for later delivery");
            // in real app, you'd queue this for retry later
            await QueueNotificationForRetry(userId, message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to {UserId}", userId);
            return false;
        }
    }

    public async Task<List<string>> GetRecommendationsAsync(string userId)
    {
        try
        {
            // different policy for recommendations - they're less critical
            var recommendationPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .FallbackAsync(
                    fallbackAction: async (ct) =>
                    {
                        _logger.LogInformation("Using fallback recommendations for {UserId}", userId);
                        var fallbackResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                        fallbackResponse.Content = new StringContent(JsonSerializer.Serialize(GetDefaultRecommendations()));
                        return fallbackResponse;
                    },
                    onFallback: (result) =>
                    {
                        _logger.LogWarning("Recommendation service failed, using defaults");
                        return Task.CompletedTask;
                    });

            var context = new Context($"GetRecommendations-{userId}");
            
            var response = await recommendationPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync($"/api/recommendations/{userId}"));

            var content = await response.Content.ReadAsStringAsync();
            var recommendations = JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();
            
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for {UserId}", userId);
            return GetDefaultRecommendations();
        }
    }

    private string GetCachedUserProfile(string userId)
    {
        // in real app, this would hit Redis or local cache
        return JsonSerializer.Serialize(new { 
            Id = userId, 
            Name = "Cached User", 
            Email = "cached@example.com",
            IsCached = true 
        });
    }

    private async Task QueueNotificationForRetry(string userId, string message)
    {
        // in real app, this would go to a queue like Azure Service Bus
        _logger.LogInformation("Queued notification for retry: User {UserId}", userId);
        await Task.CompletedTask;
    }

    private List<string> GetDefaultRecommendations()
    {
        return new List<string> 
        { 
            "Default Recommendation 1", 
            "Default Recommendation 2", 
            "Popular Content" 
        };
    }
}