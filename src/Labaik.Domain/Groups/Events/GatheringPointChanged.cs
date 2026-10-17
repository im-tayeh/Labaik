using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record GatheringPointChanged(Guid GroupId, Guid ChangedBy) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
