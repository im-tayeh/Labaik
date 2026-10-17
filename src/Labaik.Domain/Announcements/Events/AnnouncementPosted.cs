using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Common;

namespace Labaik.Domain.Announcements.Events;

// GroupId is null for a company-wide announcement (Audience = AllCompanyPilgrims).
public sealed record AnnouncementPosted(
    Guid AnnouncementId, Guid? GroupId, Guid? CompanyId, Guid AuthorId, AnnouncementAudience Audience) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
