using Labaik.Domain.Common;

namespace Labaik.Domain.Announcements;

public sealed class AnnouncementRead : Entity
{
    public Guid UserId { get; private set; }
    public DateTimeOffset ReadAtUtc { get; private set; }

    private AnnouncementRead() { } // EF

    internal AnnouncementRead(Guid userId, DateTimeOffset readAtUtc)
    {
        UserId = userId;
        ReadAtUtc = readAtUtc;
    }
}
