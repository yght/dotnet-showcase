using System.Text.Json;
using MessagePlatform.Core.Entities;
using MessagePlatform.Infrastructure.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Outbox;

public interface IOutboxService
{
    Task PublishAsync<T>(T eventData, string partitionKey = null) where T : class;
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}

// real world implementation with proper error handling
public class OutboxService : IOutboxService
{
    private readonly MessagePlatformDbContext _context;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxService> _logger;
    private static long _sequenceCounter = 0;

    public OutboxService(
        MessagePlatformDbContext context, 
        IMessagePublisher publisher,
        ILogger<OutboxService> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T eventData, string partitionKey = null) where T : class
    {
        var outboxMessage = new OutboxMessage
        {
            EventType = typeof(T).Name,
            Payload = JsonSerializer.Serialize(eventData),
            PartitionKey = partitionKey,
            SequenceNumber = Interlocked.Increment(ref _sequenceCounter)
        };

        _context.OutboxMessages.Add(outboxMessage);
        
        // this is the key - atomic transaction
        await _context.SaveChangesAsync();
        
        _logger.LogDebug("Outbox message {MessageId} stored for event {EventType}", 
            outboxMessage.Id, outboxMessage.EventType);
    }

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = await _context.OutboxMessages
            .Where(m => !m.IsProcessed && m.RetryCount < 5)
            .Where(m => m.ScheduledAt == null || m.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(m => m.SequenceNumber) // ordering matters!
            .Take(100) // batch processing
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                await _publisher.PublishAsync(message.EventType, message.Payload, message.PartitionKey);
                
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Successfully processed outbox message {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;
                
                // exponential backoff - common pattern
                if (message.RetryCount < 5)
                {
                    var delay = TimeSpan.FromMinutes(Math.Pow(2, message.RetryCount));
                    message.ScheduledAt = DateTime.UtcNow.Add(delay);
                }

                _logger.LogError(ex, "Failed to process outbox message {MessageId}, retry count: {RetryCount}", 
                    message.Id, message.RetryCount);
            }
        }

        if (pendingMessages.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

// abstraction for different message brokers
public interface IMessagePublisher
{
    Task PublishAsync(string eventType, string payload, string partitionKey = null);
}