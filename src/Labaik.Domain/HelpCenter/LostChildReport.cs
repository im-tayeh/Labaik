using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.HelpCenter.Events;
using Labaik.Domain.Map;

namespace Labaik.Domain.HelpCenter;

public enum LostChildReportStatus { Open = 1, Found = 2 }

// A pilgrim reports a lost child: approximate age, description, optional photo, where it happened,
// and whether to alert nearby app users (ShareWithNearby). The reporter's supervisors are always alerted.
public sealed class LostChildReport : AggregateRoot
{
    public const int MaxAge = 17;
    public const int DescriptionMaxLength = 1000;
    public const int LocationDescriptionMaxLength = 300;
    public const int PhotoUrlMaxLength = 2048;

    public Guid ReporterId { get; private set; }
    public int ApproxAge { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? PhotoUrl { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string? LocationDescription { get; private set; }
    public bool ShareWithNearby { get; private set; }
    public LostChildReportStatus Status { get; private set; } = LostChildReportStatus.Open;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? FoundAtUtc { get; private set; }

    private LostChildReport() { } // EF

    public static Result<LostChildReport> Create(
        Guid reporterId,
        int approxAge,
        string description,
        string? photoUrl,
        double latitude,
        double longitude,
        string? locationDescription,
        bool shareWithNearby,
        DateTimeOffset nowUtc)
    {
        if (reporterId == Guid.Empty) return HelpCenterErrors.ReporterRequired;
        if (approxAge is < 0 or > MaxAge) return HelpCenterErrors.InvalidAge;
        if (string.IsNullOrWhiteSpace(description)) return HelpCenterErrors.DescriptionRequired;
        if (!Geo.IsValid(latitude, longitude)) return MapErrors.InvalidCoordinates;

        description = description.Trim();
        locationDescription = string.IsNullOrWhiteSpace(locationDescription) ? null : locationDescription.Trim();
        photoUrl = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
        if (description.Length > DescriptionMaxLength || locationDescription is { Length: > LocationDescriptionMaxLength })
        {
            return HelpCenterErrors.TooLong;
        }

        if (photoUrl is not null && (photoUrl.Length > PhotoUrlMaxLength
            || !Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            return HelpCenterErrors.InvalidPhotoUrl;
        }

        var report = new LostChildReport
        {
            ReporterId = reporterId,
            ApproxAge = approxAge,
            Description = description,
            PhotoUrl = photoUrl,
            Latitude = latitude,
            Longitude = longitude,
            LocationDescription = locationDescription,
            ShareWithNearby = shareWithNearby,
            CreatedAtUtc = nowUtc
        };
        report.Raise(new LostChildReported(report.Id, reporterId));
        return report;
    }

    // Only the reporter closes the report. Idempotent.
    public Result MarkFound(Guid userId, DateTimeOffset nowUtc)
    {
        if (userId != ReporterId)
        {
            return Result.Failure(HelpCenterErrors.NotReporter);
        }

        if (Status == LostChildReportStatus.Open)
        {
            Status = LostChildReportStatus.Found;
            FoundAtUtc = nowUtc;
        }

        return Result.Success();
    }
}
