using Labaik.Domain.Common;

namespace Labaik.Domain.Map.Events;

public sealed record EmergencyReported(Guid AlertId, Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
