using Labaik.Domain.Common;

namespace Labaik.Domain.Rituals.Events;

public sealed record RitualStepCompleted(Guid ProgressId, Guid UserId, int StepOrder) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
