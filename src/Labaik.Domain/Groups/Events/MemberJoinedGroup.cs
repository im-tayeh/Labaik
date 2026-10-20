using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record MemberJoinedGroup(Guid GroupId, Guid UserId, Guid? CompanyId = null, Guid? AddedBySupervisorId = null) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
