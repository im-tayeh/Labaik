using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Announcements.Events;
using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Announcements;

// A broadcast to pilgrims. Own aggregate (references GroupId / CompanyId) so neither Group nor Company
// grows with every message; read receipts live here.
// - Audience Group: posted by a group supervisor; GroupId set (CompanyId = the group's company, if any).
// - Audience AllCompanyPilgrims: posted by the company; GroupId null, CompanyId set.
public sealed class Announcement : AggregateRoot
{
    public Guid? GroupId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public AnnouncementAudience Audience { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public AnnouncementImportance Importance { get; private set; }
    public DateTimeOffset? GatheringTimeUtc { get; private set; }
    public string? Location { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private readonly List<AnnouncementRead> _reads = [];
    public IReadOnlyList<AnnouncementRead> Reads => _reads.AsReadOnly();

    private Announcement() { } // EF

    private Announcement(
        Guid? groupId, Guid? companyId, AnnouncementAudience audience, Guid authorId,
        AnnouncementContent content, DateTimeOffset createdAtUtc)
    {
        GroupId = groupId;
        CompanyId = companyId;
        Audience = audience;
        AuthorId = authorId;
        Title = content.Title;
        Body = content.Body;
        Importance = content.Importance;
        GatheringTimeUtc = content.GatheringTimeUtc;
        Location = content.Location;
        ImageUrl = content.ImageUrl;
        CreatedAtUtc = createdAtUtc;

        // The author has implicitly read their own announcement.
        _reads.Add(new AnnouncementRead(authorId, createdAtUtc));
        Raise(new AnnouncementPosted(Id, groupId, companyId, authorId, audience));
    }

    // Only Group.PostAnnouncement calls this (it enforces that the author is the group's supervisor).
    internal static Result<Announcement> CreateForGroup(
        Guid groupId, Guid? companyId, Guid authorId, AnnouncementContent content, DateTimeOffset createdAtUtc)
    {
        var valid = content.Normalize();
        return valid.IsFailure
            ? valid.Error!
            : new Announcement(groupId, companyId, AnnouncementAudience.Group, authorId, valid.Value, createdAtUtc);
    }

    // Only Company.PostAnnouncement calls this (it enforces that the author owns the company).
    internal static Result<Announcement> CreateForCompany(
        Guid companyId, Guid authorId, AnnouncementContent content, DateTimeOffset createdAtUtc)
    {
        var valid = content.Normalize();
        return valid.IsFailure
            ? valid.Error!
            : new Announcement(null, companyId, AnnouncementAudience.AllCompanyPilgrims, authorId, valid.Value, createdAtUtc);
    }

    public bool IsReadBy(Guid userId) => _reads.Any(r => r.UserId == userId);

    // Idempotent: marking an already-read announcement is a no-op.
    public void MarkReadBy(Guid userId, DateTimeOffset readAtUtc)
    {
        if (!IsReadBy(userId))
        {
            _reads.Add(new AnnouncementRead(userId, readAtUtc));
        }
    }
}

// What an announcement says. Validated and trimmed by Normalize before an announcement is created.
public sealed record AnnouncementContent(
    string Title,
    string Body,
    AnnouncementImportance Importance = AnnouncementImportance.Normal,
    DateTimeOffset? GatheringTimeUtc = null,
    string? Location = null,
    string? ImageUrl = null)
{
    internal Result<AnnouncementContent> Normalize()
    {
        if (string.IsNullOrWhiteSpace(Title)) return AnnouncementErrors.TitleRequired;
        if (string.IsNullOrWhiteSpace(Body)) return AnnouncementErrors.BodyRequired;

        var title = Title.Trim();
        var body = Body.Trim();
        var location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim();

        if (title.Length > AnnouncementErrors.TitleMaxLength) return AnnouncementErrors.TitleTooLong;
        if (body.Length > AnnouncementErrors.BodyMaxLength) return AnnouncementErrors.BodyTooLong;
        if (!Enum.IsDefined(Importance)) return AnnouncementErrors.InvalidImportance;
        if (location is { Length: > AnnouncementErrors.LocationMaxLength }) return AnnouncementErrors.LocationTooLong;
        if (imageUrl is not null && (imageUrl.Length > AnnouncementErrors.ImageUrlMaxLength
            || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            return AnnouncementErrors.InvalidImageUrl;
        }

        return this with { Title = title, Body = body, Location = location, ImageUrl = imageUrl };
    }
}
