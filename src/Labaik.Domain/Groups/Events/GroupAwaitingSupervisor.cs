using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

// A company group received its first pilgrim while it still has no supervisor.
public sealed record GroupAwaitingSupervisor(Guid GroupId, Guid CompanyId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
