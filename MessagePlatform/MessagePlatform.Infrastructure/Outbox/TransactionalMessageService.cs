using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Interfaces;
using MessagePlatform.Infrastructure.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Outbox;

// this shows how to integrate outbox with business logic
public class TransactionalMessageService
{
    private readonly MessagePlatformDbContext _context;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<TransactionalMessageService> _logger;

    public TransactionalMessageService(
        MessagePlatformDbContext context,
        IOutboxService outboxService,
        ILogger<TransactionalMessageService> logger)
    {
        _context = context;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<Message> SendMessageAsync(
        string senderId, 
        string recipientId, 
        string content, 
        string? groupId = null)
    {
        // start transaction - this is the key part
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. save the message to database
            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                RecipientId = recipientId,
                Content = content,
                GroupId = groupId,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Sent
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // 2. publish to outbox (same transaction!)
            var messageEvent = new MessageSentEvent
            {
                MessageId = message.Id,
                SenderId = senderId,
                RecipientId = recipientId,
                Content = content,
                GroupId = groupId,
                Timestamp = message.Timestamp
            };

            // partition by sender for ordering guarantees
            await _outboxService.PublishAsync(messageEvent, senderId);

            // 3. commit everything together
            await transaction.CommitAsync();
            
            _logger.LogInformation("Message {MessageId} sent and queued for publishing", message.Id);
            return message;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to send message from {SenderId} to {RecipientId}", 
                senderId, recipientId);
            throw;
        }
    }

    // another example - updating message status
    public async Task MarkMessageAsReadAsync(string messageId, string userId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId);
                
            if (message == null)
            {
                throw new InvalidOperationException($"Message {messageId} not found");
            }

            message.IsRead = true;
            await _context.SaveChangesAsync();

            // publish read event
            var readEvent = new MessageReadEvent
            {
                MessageId = messageId,
                UserId = userId,
                ReadAt = DateTime.UtcNow
            };

            await _outboxService.PublishAsync(readEvent, message.SenderId);
            await transaction.CommitAsync();
            
            _logger.LogDebug("Message {MessageId} marked as read by {UserId}", messageId, userId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to mark message {MessageId} as read", messageId);
            throw;
        }
    }
}

// events that go through outbox
public class MessageSentEvent
{
    public string MessageId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MessageReadEvent
{
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
}