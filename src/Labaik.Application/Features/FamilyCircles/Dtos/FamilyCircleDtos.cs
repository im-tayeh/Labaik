using Labaik.Domain.FamilyCircles;

namespace Labaik.Application.Features.FamilyCircles.Dtos;

// Role 1 Owner / 2 Member.
public sealed record FamilyCircleSummaryDto(Guid Id, string Name, string JoinCode, FamilyCircleRole MyRole, int MemberCount);

public sealed record FamilyCircleMemberDto(
    Guid UserId,
    string? FullName,
    string? PhotoUrl,
    FamilyCircleRole Role,
    bool IsSharingLocation,
    DateTimeOffset JoinedAtUtc);

public sealed record FamilyCircleDto(
    Guid Id,
    string Name,
    string JoinCode,
    Guid OwnerId,
    FamilyCircleRole MyRole,
    IReadOnlyList<FamilyCircleMemberDto> Members);

// "Join circle?" confirmation screen.
public sealed record FamilyCirclePreviewDto(Guid Id, string Name, string? OwnerName, int MemberCount);

// "View location" in the circle. Position fields are null unless the member shows their location in this
// circle AND has location sharing on.
public sealed record FamilyMemberLocationDto(
    Guid UserId,
    string? FullName,
    bool IsSharing,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    DateTimeOffset? UpdatedAtUtc,
    bool IsStale);

public sealed record FamilyLocationSharingDto(Guid CircleId, bool IsSharingLocation);
