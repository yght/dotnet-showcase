using HotChocolate;
using MediatR;
using MessagePlatform.Core.Commands;
using MessagePlatform.Core.Entities;

namespace MessagePlatform.API.GraphQL;

[ExtendObjectType("Mutation")]
public class MessageMutations
{
    public async Task<Message> SendMessage(
        [Service] IMediator mediator,
        string senderId,
        string recipientId,
        string content,
        string? groupId = null,
        string? replyToMessageId = null)
    {
        var command = new CreateMessageCommand(senderId, recipientId, content, groupId, replyToMessageId);
        return await mediator.Send(command);
    }

    public async Task<User> CreateUser(
        [Service] IRepository<User> repository,
        string username,
        string email,
        string displayName)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsOnline = false,
            Status = UserStatus.Available
        };

        return await repository.AddAsync(user);
    }

    public async Task<Group> CreateGroup(
        [Service] IRepository<Group> repository,
        string name,
        string description,
        string createdBy,
        bool isPublic = false)
    {
        var group = new Group
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsPublic = isPublic,
            LastActivityAt = DateTime.UtcNow
        };

        group.MemberIds.Add(createdBy);
        group.AdminIds.Add(createdBy);

        return await repository.AddAsync(group);
    }
}