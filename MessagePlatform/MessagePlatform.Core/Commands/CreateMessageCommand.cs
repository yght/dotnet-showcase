using MediatR;
using MessagePlatform.Core.Entities;

namespace MessagePlatform.Core.Commands;

public record CreateMessageCommand(
    string SenderId,
    string RecipientId,
    string Content,
    string? GroupId = null,
    string? ReplyToMessageId = null
) : IRequest<Message>;

public class CreateMessageCommandHandler : IRequestHandler<CreateMessageCommand, Message>
{
    private readonly IRepository<Message> _messageRepository;

    public CreateMessageCommandHandler(IRepository<Message> messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<Message> Handle(CreateMessageCommand request, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = request.SenderId,
            RecipientId = request.RecipientId,
            Content = request.Content,
            GroupId = request.GroupId,
            ReplyToMessageId = request.ReplyToMessageId,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        return await _messageRepository.AddAsync(message);
    }
}