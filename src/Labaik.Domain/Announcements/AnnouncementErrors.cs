using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Announcements;

public static class AnnouncementErrors
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;

    public static Error TitleRequired =>
        Error.Validation("Announcement.TitleRequired", "Announcement title is required.");

    public static Error BodyRequired =>
        Error.Validation("Announcement.BodyRequired", "Announcement body is required.");

    public static Error TitleTooLong =>
        Error.Validation("Announcement.TitleTooLong", $"Announcement title must be at most {TitleMaxLength} characters.");

    public static Error BodyTooLong =>
        Error.Validation("Announcement.BodyTooLong", $"Announcement body must be at most {BodyMaxLength} characters.");

    public static Error NotFound =>
        Error.NotFound("Announcement.NotFound", "The announcement was not found.");
}
