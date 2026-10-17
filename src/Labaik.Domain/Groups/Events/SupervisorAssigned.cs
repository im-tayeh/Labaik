using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record SupervisorAssigned(Guid GroupId, Guid SupervisorId, Guid? CompanyId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
