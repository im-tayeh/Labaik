using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record MemberRemovedFromGroup(Guid GroupId, Guid UserId, Guid CompanyId, Guid RemovedBySupervisorId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
