using Polly;
using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;

namespace MessagePlatform.Infrastructure.Resilience;

// this shows how to apply circuit breakers to database operations
public class ResilientDatabaseService
{
    private readonly IRepository<Message> _messageRepo;
    private readonly ILogger<ResilientDatabaseService> _logger;
    private readonly IAsyncPolicy _databasePolicy;

    public ResilientDatabaseService(
        IRepository<Message> messageRepo, 
        ILogger<ResilientDatabaseService> logger)
    {
        _messageRepo = messageRepo;
        _logger = logger;

        // database-specific resilience policy
        _databasePolicy = Policy
            .Handle<SqlException>(ex => IsTransientError(ex))
            .Or<TimeoutException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("timeout"))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Database retry {RetryCount} after {Delay}s. Error: {Error}",
                        retryCount, timeSpan.TotalSeconds, exception.Exception?.Message);
                })
            .WrapAsync(Policy
                .Handle<SqlException>()
                .Or<TimeoutException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(2),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError("Database circuit breaker opened for {Duration}m. Last error: {Error}",
                            duration.TotalMinutes, exception.Exception?.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Database circuit breaker reset - database appears healthy");
                    }));
    }

    public async Task<Message?> GetMessageSafelyAsync(string messageId)
    {
        try
        {
            return await _databasePolicy.ExecuteAsync(async () =>
            {
                return await _messageRepo.GetByIdAsync(messageId);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message {MessageId} even with retries", messageId);
            return null;
        }
    }

    public async Task<bool> SaveMessageSafelyAsync(Message message)
    {
        try
        {
            await _databasePolicy.ExecuteAsync(async () =>
            {
                await _messageRepo.AddAsync(message);
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message {MessageId} even with retries", message.Id);
            
            // fallback: save to local cache or queue for later
            await SaveToFallbackStorage(message);
            return false;
        }
    }

    public async Task<List<Message>> GetUserMessagesSafelyAsync(string userId)
    {
        try
        {
            return (await _databasePolicy.ExecuteAsync(async () =>
            {
                return await _messageRepo.FindAsync(m => 
                    m.SenderId == userId || m.RecipientId == userId);
            })).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for user {UserId}", userId);
            
            // return cached messages if available
            return await GetCachedMessages(userId);
        }
    }

    // health check method
    public async Task<bool> IsDatabaseHealthyAsync()
    {
        try
        {
            await _databasePolicy.ExecuteAsync(async () =>
            {
                // simple health check query
                await _messageRepo.FindAsync(m => m.Id == "health-check");
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Database health check failed: {Error}", ex.Message);
            return false;
        }
    }

    private static bool IsTransientError(SqlException ex)
    {
        // SQL Server transient error codes
        var transientErrors = new int[]
        {
            2,      // Timeout
            53,     // Network error
            121,    // Semaphore timeout
            1205,   // Deadlock
            1222,   // Lock request timeout
            49918,  // Cannot process request
            49919,  // Cannot process create or update request
            49920   // Cannot process request
        };

        return transientErrors.Contains(ex.Number);
    }

    private async Task SaveToFallbackStorage(Message message)
    {
        // in real app, this would save to Redis, local file, or queue
        _logger.LogInformation("Saved message {MessageId} to fallback storage", message.Id);
        await Task.CompletedTask;
    }

    private async Task<List<Message>> GetCachedMessages(string userId)
    {
        // in real app, this would check Redis cache
        _logger.LogInformation("Returning cached messages for user {UserId}", userId);
        await Task.CompletedTask;
        return new List<Message>();
    }
}