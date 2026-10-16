using Labaik.Domain.Groups.Enums;

namespace Labaik.Application.Features.Locations.Dtos;

public sealed record MyLocationSharingDto(
    bool IsSharing,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? UpdatedAtUtc,
    bool IsStale);

// One row of "where are my group members"; position fields are null when the member is not sharing.
public sealed record MemberLocationDto(
    Guid UserId,
    string? FullName,
    GroupMemberRole Role,
    bool IsSharing,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? UpdatedAtUtc,
    bool IsStale);

// Realtime push payload (the client already has names/roles from the member list).
public sealed record LiveLocationDto(
    Guid UserId,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    DateTimeOffset UpdatedAtUtc);
