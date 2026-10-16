using Labaik.Domain.Common;

namespace Labaik.Domain.Chat.Events;

public sealed record ChatMessageSent(Guid MessageId, Guid GroupId, Guid SenderId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
