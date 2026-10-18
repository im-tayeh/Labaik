using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Queries.GetGroupMembers;

// Member list for the group's supervisor (GET /groups/{groupId}/members).
public sealed record GetGroupMembersQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<GroupMemberRowDto>>>;

public sealed class GetGroupMembersQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetGroupMembersQuery, Result<IReadOnlyList<GroupMemberRowDto>>>
{
    public async Task<Result<IReadOnlyList<GroupMemberRowDto>>> Handle(GetGroupMembersQuery request, CancellationToken cancellationToken)
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

        return Result.Success<IReadOnlyList<GroupMemberRowDto>>(
            await context.GetMemberRowsAsync(group, userDirectory, cancellationToken));
    }
}

// Member list for the company that owns the group (GET /company/groups/{groupId}/members).
public sealed record GetCompanyGroupMembersQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<GroupMemberRowDto>>>;

public sealed class GetCompanyGroupMembersQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetCompanyGroupMembersQuery, Result<IReadOnlyList<GroupMemberRowDto>>>
{
    public async Task<Result<IReadOnlyList<GroupMemberRowDto>>> Handle(GetCompanyGroupMembersQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var group = await context.GetCompanyGroupAsync(request.GroupId, companyId.Value, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        return Result.Success<IReadOnlyList<GroupMemberRowDto>>(
            await context.GetMemberRowsAsync(group, userDirectory, cancellationToken));
    }
}
