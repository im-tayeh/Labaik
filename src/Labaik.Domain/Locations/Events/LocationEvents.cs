using Labaik.Domain.Common;

namespace Labaik.Domain.Locations.Events;

public sealed record MemberLocationUpdated(
    Guid UserId, double Latitude, double Longitude, double? AccuracyMeters, DateTimeOffset UpdatedAtUtc) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}

public sealed record MemberStoppedSharingLocation(Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
