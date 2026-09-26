using Labaik.Domain.Common;

namespace Labaik.Domain.Journeys.Events;

public sealed record JourneyCreated(Guid JourneyId, Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
