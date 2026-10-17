using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

// A pilgrim confirmed "I arrived" at the gathering.
public sealed record PilgrimArrived(Guid GroupId, Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
