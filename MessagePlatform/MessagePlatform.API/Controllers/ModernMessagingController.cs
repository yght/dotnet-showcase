using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MediatR;
using MessagePlatform.Core.Commands;
using MessagePlatform.Core.Queries;
using MessagePlatform.API.Hubs;
using MessagePlatform.Infrastructure.EventSourcing;
using System.Diagnostics;

namespace MessagePlatform.API.Controllers;

[ApiController]
[Route("api/v2/[controller]")]
public class ModernMessagingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<MessageHub> _hubContext;
    private readonly IEventStore _eventStore;
    private readonly ILogger<ModernMessagingController> _logger;
    private static readonly ActivitySource ActivitySource = new("MessagePlatform.API");

    public ModernMessagingController(
        IMediator mediator,
        IHubContext<MessageHub> hubContext,
        IEventStore eventStore,
        ILogger<ModernMessagingController> logger)
    {
        _mediator = mediator;
        _hubContext = hubContext;
        _eventStore = eventStore;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        using var activity = ActivitySource.StartActivity("SendMessage");
        activity?.SetTag("senderId", request.SenderId);
        activity?.SetTag("recipientId", request.RecipientId);

        try
        {
            var command = new CreateMessageCommand(
                request.SenderId, 
                request.RecipientId, 
                request.Content, 
                request.GroupId, 
                request.ReplyToMessageId);
            
            var message = await _mediator.Send(command);

            // Store event for event sourcing
            var messageEvent = new MessageSentEvent(
                message.Id,
                DateTime.UtcNow,
                1,
                message.SenderId,
                message.RecipientId,
                message.Content,
                message.GroupId,
                message.ReplyToMessageId);
            
            await _eventStore.AppendEventsAsync($"Message-{message.Id}", new[] { messageEvent }, -1);

            // Send real-time notification via SignalR
            if (!string.IsNullOrEmpty(request.GroupId))
            {
                await _hubContext.Clients.Group(request.GroupId).SendAsync("ReceiveMessage", message);
            }
            else
            {
                await _hubContext.Clients.User(request.RecipientId).SendAsync("ReceiveMessage", message);
            }

            _logger.LogInformation("Message {MessageId} sent successfully from {SenderId} to {RecipientId}", 
                message.Id, request.SenderId, request.RecipientId);

            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message from {SenderId} to {RecipientId}", 
                request.SenderId, request.RecipientId);
            return StatusCode(500, "Failed to send message");
        }
    }

    [HttpGet("user/{userId}/messages")]
    public async Task<IActionResult> GetUserMessages(string userId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        using var activity = ActivitySource.StartActivity("GetUserMessages");
        activity?.SetTag("userId", userId);
        activity?.SetTag("skip", skip);
        activity?.SetTag("take", take);

        var query = new GetUserMessagesQuery(userId, skip, take);
        var messages = await _mediator.Send(query);
        
        return Ok(messages);
    }

    [HttpGet("events/{messageId}")]
    public async Task<IActionResult> GetMessageEvents(string messageId)
    {
        using var activity = ActivitySource.StartActivity("GetMessageEvents");
        activity?.SetTag("messageId", messageId);

        var events = await _eventStore.GetEventsAsync($"Message-{messageId}");
        return Ok(events);
    }

    [HttpPost("broadcast/{groupId}")]
    public async Task<IActionResult> BroadcastToGroup(string groupId, [FromBody] BroadcastRequest request)
    {
        using var activity = ActivitySource.StartActivity("BroadcastToGroup");
        activity?.SetTag("groupId", groupId);
        activity?.SetTag("messageType", request.Type);

        await _hubContext.Clients.Group(groupId).SendAsync(request.Type, request.Data);
        
        _logger.LogInformation("Broadcast message of type {MessageType} sent to group {GroupId}", 
            request.Type, groupId);
        
        return Ok();
    }
}

public record SendMessageRequest(
    string SenderId,
    string RecipientId,
    string Content,
    string? GroupId = null,
    string? ReplyToMessageId = null
);

public record BroadcastRequest(
    string Type,
    object Data
);