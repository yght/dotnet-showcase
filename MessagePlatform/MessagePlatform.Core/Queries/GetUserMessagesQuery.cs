using MediatR;
using MessagePlatform.Core.Entities;

namespace MessagePlatform.Core.Queries;

public record GetUserMessagesQuery(string UserId, int Skip = 0, int Take = 50) : IRequest<IEnumerable<Message>>;

public class GetUserMessagesQueryHandler : IRequestHandler<GetUserMessagesQuery, IEnumerable<Message>>
{
    private readonly IRepository<Message> _messageRepository;

    public GetUserMessagesQueryHandler(IRepository<Message> messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<IEnumerable<Message>> Handle(GetUserMessagesQuery request, CancellationToken cancellationToken)
    {
        var messages = await _messageRepository.FindAsync(
            m => m.SenderId == request.UserId || m.RecipientId == request.UserId);
        
        return messages
            .OrderByDescending(m => m.Timestamp)
            .Skip(request.Skip)
            .Take(request.Take);
    }
}