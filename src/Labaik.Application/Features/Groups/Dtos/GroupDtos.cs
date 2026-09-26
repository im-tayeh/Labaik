using Labaik.Domain.Groups.Enums;

namespace Labaik.Application.Features.Groups.Dtos;

public sealed record GroupMemberDto(Guid UserId, GroupMemberRole Role, DateTimeOffset JoinedAtUtc);

public sealed record GroupDto(Guid Id, string Name, string JoinCode, Guid LeaderId, IReadOnlyList<GroupMemberDto> Members);

// For the "confirm group" preview screen.
public sealed record GroupSummaryDto(Guid Id, string Name, string? LeaderName, int MemberCount);