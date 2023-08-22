using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace MessagePlatform.Infrastructure.Resilience;

// monitors circuit breaker states and logs important events
public class CircuitBreakerMonitor : BackgroundService
{
    private readonly ILogger<CircuitBreakerMonitor> _logger;
    private readonly Dictionary<string, CircuitBreakerState> _circuitStates;

    public CircuitBreakerMonitor(ILogger<CircuitBreakerMonitor> logger)
    {
        _logger = logger;
        _circuitStates = new Dictionary<string, CircuitBreakerState>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorCircuitBreakers();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in circuit breaker monitoring");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task MonitorCircuitBreakers()
    {
        // in real app, you'd check actual circuit breaker states
        // this is a simplified example
        
        var services = new[] { "UserService", "NotificationService", "AnalyticsService" };
        
        foreach (var service in services)
        {
            var currentState = SimulateCircuitBreakerState();
            
            if (_circuitStates.TryGetValue(service, out var previousState))
            {
                if (previousState != currentState)
                {
                    _logger.LogInformation("Circuit breaker state changed for {Service}: {OldState} -> {NewState}",
                        service, previousState, currentState);
                        
                    if (currentState == CircuitBreakerState.Open)
                    {
                        _logger.LogWarning("⚠️  Circuit breaker OPENED for {Service} - service degraded", service);
                    }
                    else if (currentState == CircuitBreakerState.Closed && previousState == CircuitBreakerState.Open)
                    {
                        _logger.LogInformation("✅ Circuit breaker CLOSED for {Service} - service recovered", service);
                    }
                }
            }
            
            _circuitStates[service] = currentState;
        }

        await Task.CompletedTask;
    }

    private CircuitBreakerState SimulateCircuitBreakerState()
    {
        // in real app, you'd get this from actual circuit breaker instances
        var states = Enum.GetValues<CircuitBreakerState>();
        return states[Random.Shared.Next(states.Length)];
    }

    public Dictionary<string, object> GetCircuitBreakerStatus()
    {
        return _circuitStates.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)new
            {
                State = kvp.Value.ToString(),
                IsHealthy = kvp.Value == CircuitBreakerState.Closed,
                LastChecked = DateTime.UtcNow
            });
    }
}