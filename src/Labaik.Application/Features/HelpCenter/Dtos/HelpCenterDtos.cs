using Labaik.Domain.HelpCenter;

namespace Labaik.Application.Features.HelpCenter.Dtos;

// "Contact my supervisor" card in the help center.
public sealed record SupervisorContactDto(
    Guid GroupId,
    string GroupName,
    Guid SupervisorId,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string? PhotoUrl);

// Status 1 Open / 2 Found.
public sealed record LostChildReportDto(
    Guid Id,
    Guid ReporterId,
    string? ReporterName,
    int ApproxAge,
    string Description,
    string? PhotoUrl,
    double Latitude,
    double Longitude,
    string? LocationDescription,
    bool ShareWithNearby,
    LostChildReportStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FoundAtUtc);

public static class LostChildReportMapper
{
    public static LostChildReportDto ToDto(this LostChildReport r, string? reporterName) => new(
        r.Id, r.ReporterId, reporterName, r.ApproxAge, r.Description, r.PhotoUrl, r.Latitude, r.Longitude,
        r.LocationDescription, r.ShareWithNearby, r.Status, r.CreatedAtUtc, r.FoundAtUtc);
}
