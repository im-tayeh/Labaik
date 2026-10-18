using Labaik.Domain.Common;

namespace Labaik.Domain.FamilyCircles.Events;

public sealed record FamilyMemberJoined(Guid CircleId, Guid UserId, Guid OwnerId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}

// A member showed or hid their live location in the circle (also raised with false when a sharer is removed).
public sealed record FamilyLocationSharingChanged(Guid CircleId, Guid UserId, bool IsSharing) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
