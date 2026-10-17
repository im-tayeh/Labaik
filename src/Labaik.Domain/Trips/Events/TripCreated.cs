using Labaik.Domain.Common;

namespace Labaik.Domain.Trips.Events;

public sealed record TripCreated(Guid TripId, Guid CompanyId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
