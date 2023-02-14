using Microsoft.AspNetCore.Mvc;
using MessagePlatform.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.API.Controllers;

[ApiController]
[Route("api/reliable-messaging")]
public class ReliableMessagingController : ControllerBase
{
    private readonly TransactionalMessageService _messageService;
    private readonly ILogger<ReliableMessagingController> _logger;

    public ReliableMessagingController(
        TransactionalMessageService messageService,
        ILogger<ReliableMessagingController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            // this method guarantees that either both database write AND 
            // message publishing succeed, or both fail
            var message = await _messageService.SendMessageAsync(
                request.SenderId,
                request.RecipientId, 
                request.Content,
                request.GroupId);

            return Ok(new
            {
                MessageId = message.Id,
                Status = "Sent",
                Note = "Message saved and queued for reliable delivery"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reliable message");
            return StatusCode(500, "Failed to send message");
        }
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead([FromBody] MarkReadRequest request)
    {
        try
        {
            await _messageService.MarkMessageAsReadAsync(request.MessageId, request.UserId);
            
            return Ok(new
            {
                Status = "Updated",
                Note = "Read status updated and event published reliably"
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message as read");
            return StatusCode(500, "Failed to update message status");
        }
    }

    // endpoint to check outbox health - useful for monitoring
    [HttpGet("outbox/health")]
    public async Task<IActionResult> GetOutboxHealth()
    {
        // in real systems you'd inject the DbContext or a health service
        return Ok(new
        {
            Status = "Healthy",
            Note = "Use this endpoint to monitor outbox processing delays",
            Recommendation = "Alert if pending messages > 1000 or oldest > 5 minutes"
        });
    }
}

public record SendMessageRequest(
    string SenderId,
    string RecipientId,
    string Content,
    string? GroupId = null
);

public record MarkReadRequest(
    string MessageId,
    string UserId
);