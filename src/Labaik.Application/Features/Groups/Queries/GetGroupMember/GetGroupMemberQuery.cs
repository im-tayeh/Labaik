using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Profiles.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetGroupMember;

// Supervisor-only member card. Contains HealthNotes (sensitive): never reuse this DTO in lists or
// in anything a pilgrim or another member can read.
public sealed record GroupMemberDetailsDto(
    Guid UserId,
    string? FullName,
    string? Email,
    GroupMemberRole Role,
    int? MemberNumberInGroup,
    Gender? Gender,
    string? Nationality,
    string? PhotoUrl,
    string? HealthNotes,
    string? Notes,
    AttendanceStatus AttendanceStatus,
    DateTimeOffset JoinedAtUtc);

public sealed record GetGroupMemberQuery(Guid GroupId, Guid UserId) : IRequest<Result<GroupMemberDetailsDto>>;

public sealed class GetGroupMemberQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetGroupMemberQuery, Result<GroupMemberDetailsDto>>
{
    public async Task<Result<GroupMemberDetailsDto>> Handle(GetGroupMemberQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.GetGroupForMemberAsync(request.GroupId, userId, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        if (group.LeaderId != userId)
        {
            return GroupErrors.NotLeader;
        }

        var member = group.Members.FirstOrDefault(m => m.UserId == request.UserId);
        if (member is null)
        {
            return GroupErrors.MemberNotFound;
        }

        var contact = await userDirectory.GetContactAsync(member.UserId, cancellationToken);
        var profile = await context.PilgrimProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == member.UserId, cancellationToken);

        return new GroupMemberDetailsDto(
            member.UserId, contact?.FullName, contact?.Email, member.Role, member.MemberNumber,
            profile?.Gender, profile?.Nationality, profile?.PhotoUrl, profile?.HealthNotes,
            member.Notes, member.AttendanceStatus, member.JoinedAtUtc);
    }
}
