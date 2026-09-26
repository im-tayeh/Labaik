using Labaik.Domain.Common;

namespace Labaik.Domain.Journeys.Events;

public sealed record JourneyReadied(Guid JourneyId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}