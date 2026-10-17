using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

// LeaderId is null for a company-managed group created without a supervisor.
public sealed record GroupCreated(Guid GroupId, Guid? LeaderId, Guid? CompanyId, Guid? TripId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
