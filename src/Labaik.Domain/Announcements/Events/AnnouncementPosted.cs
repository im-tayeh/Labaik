using Labaik.Domain.Common;

namespace Labaik.Domain.Announcements.Events;

public sealed record AnnouncementPosted(Guid AnnouncementId, Guid GroupId, Guid AuthorId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
