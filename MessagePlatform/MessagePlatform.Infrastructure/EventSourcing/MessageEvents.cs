using MessagePlatform.Core.Entities;

namespace MessagePlatform.Infrastructure.EventSourcing;

public abstract record DomainEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version
);

public record MessageSentEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version,
    string SenderId,
    string RecipientId,
    string Content,
    string? GroupId,
    string? ReplyToMessageId
) : DomainEvent(AggregateId, Timestamp, Version);

public record MessageEditedEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version,
    string NewContent,
    DateTime EditedAt
) : DomainEvent(AggregateId, Timestamp, Version);

public record MessageDeletedEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version,
    DateTime DeletedAt
) : DomainEvent(AggregateId, Timestamp, Version);

public record MessageReactionAddedEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version,
    string UserId,
    string Emoji,
    DateTime ReactedAt
) : DomainEvent(AggregateId, Timestamp, Version);

public record MessageReadEvent(
    string AggregateId,
    DateTime Timestamp,
    int Version,
    string UserId,
    DateTime ReadAt
) : DomainEvent(AggregateId, Timestamp, Version);