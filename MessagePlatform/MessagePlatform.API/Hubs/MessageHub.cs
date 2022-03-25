using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using MessagePlatform.Core.Commands;

namespace MessagePlatform.API.Hubs;

[Authorize]
public class MessageHub : Hub
{
    private readonly IMediator _mediator;
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(IMediator mediator, ILogger<MessageHub> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        await Clients.Group(groupId).SendAsync("UserJoined", Context.UserIdentifier);
        _logger.LogInformation("User {UserId} joined group {GroupId}", Context.UserIdentifier, groupId);
    }

    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
        await Clients.Group(groupId).SendAsync("UserLeft", Context.UserIdentifier);
        _logger.LogInformation("User {UserId} left group {GroupId}", Context.UserIdentifier, groupId);
    }

    public async Task SendMessage(string recipientId, string content, string? groupId = null)
    {
        var senderId = Context.UserIdentifier!;
        
        var command = new CreateMessageCommand(senderId, recipientId, content, groupId);
        var message = await _mediator.Send(command);

        if (!string.IsNullOrEmpty(groupId))
        {
            await Clients.Group(groupId).SendAsync("ReceiveMessage", message);
        }
        else
        {
            await Clients.User(recipientId).SendAsync("ReceiveMessage", message);
            await Clients.Caller.SendAsync("MessageSent", message);
        }

        _logger.LogInformation("Message sent from {SenderId} to {RecipientId}", senderId, recipientId);
    }

    public async Task StartTyping(string recipientId, string? groupId = null)
    {
        var senderId = Context.UserIdentifier!;
        
        if (!string.IsNullOrEmpty(groupId))
        {
            await Clients.GroupExcept(groupId, Context.ConnectionId).SendAsync("UserTyping", senderId);
        }
        else
        {
            await Clients.User(recipientId).SendAsync("UserTyping", senderId);
        }
    }

    public async Task StopTyping(string recipientId, string? groupId = null)
    {
        var senderId = Context.UserIdentifier!;
        
        if (!string.IsNullOrEmpty(groupId))
        {
            await Clients.GroupExcept(groupId, Context.ConnectionId).SendAsync("UserStoppedTyping", senderId);
        }
        else
        {
            await Clients.User(recipientId).SendAsync("UserStoppedTyping", senderId);
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", 
            Context.UserIdentifier, Context.ConnectionId);
        
        await Clients.Others.SendAsync("UserOnline", Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("User {UserId} disconnected", Context.UserIdentifier);
        
        await Clients.Others.SendAsync("UserOffline", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}