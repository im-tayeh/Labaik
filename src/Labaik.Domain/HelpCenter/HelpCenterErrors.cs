using Labaik.Domain.Common.Results;

namespace Labaik.Domain.HelpCenter;

public static class HelpCenterErrors
{
    public static Error ReporterRequired =>
        Error.Validation("LostChild.ReporterRequired", "A reporter is required.");

    public static Error InvalidAge =>
        Error.Validation("LostChild.InvalidAge", $"Approximate age must be between 0 and {LostChildReport.MaxAge}.");

    public static Error DescriptionRequired =>
        Error.Validation("LostChild.DescriptionRequired", "A description of the child is required.");

    public static Error TooLong =>
        Error.Validation("LostChild.TooLong", "Description or location text is too long.");

    public static Error InvalidPhotoUrl =>
        Error.Validation("LostChild.InvalidPhotoUrl", "Photo URL must be an absolute https URL.");

    public static Error NotReporter =>
        Error.Forbidden("LostChild.NotReporter", "Only the reporter can close this report.");

    public static Error NotFound =>
        Error.NotFound("LostChild.NotFound", "Report not found.");
}
