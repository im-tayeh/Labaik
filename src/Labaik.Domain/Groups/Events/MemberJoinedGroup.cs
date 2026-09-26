using Labaik.Domain.Common;

namespace Labaik.Domain.Groups.Events;

public sealed record MemberJoinedGroup(Guid GroupId, Guid UserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}