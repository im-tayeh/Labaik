using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Announcements;

public static class AnnouncementErrors
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;
    public const int LocationMaxLength = 300;
    public const int ImageUrlMaxLength = 500;

    public static Error InvalidImportance =>
        Error.Validation("Announcement.InvalidImportance", "Importance must be 1 (normal) or 2 (important).");

    public static Error LocationTooLong =>
        Error.Validation("Announcement.LocationTooLong", $"Location must be at most {LocationMaxLength} characters.");

    public static Error InvalidImageUrl =>
        Error.Validation("Announcement.InvalidImageUrl", "Image URL must be an absolute https URL.");

    public static Error NotCompanyOwner =>
        Error.Forbidden("Announcement.NotCompanyOwner", "Only the company account can announce to all its pilgrims.");

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
