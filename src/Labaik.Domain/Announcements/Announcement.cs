using Labaik.Domain.Announcements.Events;
using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Announcements;

// A broadcast from a group leader to the group's members. Own aggregate (references GroupId)
// so the Group aggregate does not grow with every message; read receipts live here.
public sealed class Announcement : AggregateRoot
{
    public Guid GroupId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private readonly List<AnnouncementRead> _reads = [];
    public IReadOnlyList<AnnouncementRead> Reads => _reads.AsReadOnly();

    private Announcement() { } // EF

    private Announcement(Guid groupId, Guid authorId, string title, string body, DateTimeOffset createdAtUtc)
    {
        GroupId = groupId;
        AuthorId = authorId;
        Title = title;
        Body = body;
        CreatedAtUtc = createdAtUtc;

        // The author has implicitly read their own announcement.
        _reads.Add(new AnnouncementRead(authorId, createdAtUtc));
        Raise(new AnnouncementPosted(Id, groupId, authorId));
    }

    // Only Group.PostAnnouncement creates announcements (it enforces that the author is the leader).
    internal static Result<Announcement> Create(
        Guid groupId, Guid authorId, string title, string body, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title)) return AnnouncementErrors.TitleRequired;
        if (string.IsNullOrWhiteSpace(body)) return AnnouncementErrors.BodyRequired;

        title = title.Trim();
        body = body.Trim();

        if (title.Length > AnnouncementErrors.TitleMaxLength) return AnnouncementErrors.TitleTooLong;
        if (body.Length > AnnouncementErrors.BodyMaxLength) return AnnouncementErrors.BodyTooLong;

        return new Announcement(groupId, authorId, title, body, createdAtUtc);
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
