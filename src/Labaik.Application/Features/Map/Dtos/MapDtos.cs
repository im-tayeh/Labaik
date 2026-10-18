using Labaik.Domain.Map;

namespace Labaik.Application.Features.Map.Dtos;

// Type 1 MainGate / 2 SubGate / 3 Facility; facilityKind (facilities only) 1 Restrooms / 2 Medical /
// 3 ZamzamWater / 4 LostAndFound / 5 WheelchairService. Name/description are Arabic seed content.
public sealed record MapPlaceDto(
    Guid Id,
    string Key,
    MapPlaceType Type,
    FacilityKind? FacilityKind,
    string Name,
    int? Number,
    string? Description,
    double Latitude,
    double Longitude,
    string? ImageUrl);

public sealed record MapPointDto(double Latitude, double Longitude);

// Straight-line guidance only (distance, compass bearing, walking estimate at 1.2 m/s).
// Turn-by-turn routing and drawing the path are the client map SDK's job.
public sealed record MapRouteDto(
    MapPointDto From,
    MapPointDto To,
    MapPlaceDto? Destination,
    int DistanceMeters,
    int BearingDegrees,
    int WalkingMinutes);

// Status 1 Open / 2 Resolved.
public sealed record EmergencyAlertDto(
    Guid Id,
    Guid UserId,
    string? FullName,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    string? Note,
    EmergencyStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public static class MapMapper
{
    public static MapPlaceDto ToDto(this MapPlace p) => new(
        p.Id, p.Key, p.Type, p.FacilityKind, p.Name, p.Number, p.Description, p.Latitude, p.Longitude, p.ImageUrl);

    public static EmergencyAlertDto ToDto(this EmergencyAlert a, string? fullName) => new(
        a.Id, a.UserId, fullName, a.Latitude, a.Longitude, a.AccuracyMeters, a.Note, a.Status, a.CreatedAtUtc, a.ResolvedAtUtc);
}
