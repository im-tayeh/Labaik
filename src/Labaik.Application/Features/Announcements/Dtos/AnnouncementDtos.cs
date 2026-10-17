using System.Linq.Expressions;
using Labaik.Domain.Announcements;
using Labaik.Domain.Announcements.Enums;

namespace Labaik.Application.Features.Announcements.Dtos;

// GroupId is null for a company-wide announcement (Audience = AllCompanyPilgrims).
public sealed record AnnouncementDto(
    Guid Id,
    Guid? GroupId,
    string Title,
    string Body,
    Guid AuthorId,
    string? AuthorName,
    DateTimeOffset CreatedAtUtc,
    bool IsRead,
    AnnouncementImportance Importance,
    AnnouncementAudience Audience,
    DateTimeOffset? GatheringTimeUtc,
    string? Location,
    string? ImageUrl);

public static class AnnouncementVisibility
{
    // Announcements a member of the given group sees: the group's own, plus its company's company-wide ones.
    public static Expression<Func<Announcement, bool>> VisibleInGroup(Guid groupId, Guid? companyId) =>
        a => a.GroupId == groupId
             || (companyId != null && a.Audience == AnnouncementAudience.AllCompanyPilgrims && a.CompanyId == companyId);
}

public static class AnnouncementMapper
{
    public static AnnouncementDto ToDto(this Announcement a, string? authorName, bool isRead) => new(
        a.Id, a.GroupId, a.Title, a.Body, a.AuthorId, authorName, a.CreatedAtUtc, isRead,
        a.Importance, a.Audience, a.GatheringTimeUtc, a.Location, a.ImageUrl);
}
