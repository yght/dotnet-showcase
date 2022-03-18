using HotChocolate;
using HotChocolate.Data;
using MediatR;
using MessagePlatform.Core.Entities;
using MessagePlatform.Core.Queries;

namespace MessagePlatform.API.GraphQL;

[ExtendObjectType("Query")]
public class MessageQueries
{
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Message>> GetMessages(
        [Service] IMediator mediator,
        string userId,
        int skip = 0,
        int take = 50)
    {
        return await mediator.Send(new GetUserMessagesQuery(userId, skip, take));
    }

    [UseProjection]
    public async Task<Message?> GetMessage(
        [Service] IRepository<Message> repository, 
        string id)
    {
        return await repository.GetByIdAsync(id);
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<User>> GetUsers([Service] IRepository<User> repository)
    {
        return await repository.GetAllAsync();
    }

    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public async Task<IEnumerable<Group>> GetGroups([Service] IRepository<Group> repository)
    {
        return await repository.GetAllAsync();
    }
}