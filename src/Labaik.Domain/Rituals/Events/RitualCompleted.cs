using Labaik.Domain.Common;
using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Rituals.Events;

public sealed record RitualCompleted(Guid ProgressId, Guid UserId, PilgrimageType PilgrimageType) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
