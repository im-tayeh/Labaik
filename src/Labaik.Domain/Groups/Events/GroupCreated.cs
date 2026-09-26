using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record GroupCreated(Guid GroupId, Guid LeaderId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
