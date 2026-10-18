using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Queries.GetGroupMember;

// Member card for the group's supervisor (GET /groups/{groupId}/members/{userId}).
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

        var card = await context.GetMemberDetailsAsync(group, request.UserId, userDirectory, cancellationToken);
        return card is null ? GroupErrors.MemberNotFound : card;
    }
}

// Member card for the company that owns the group (GET /company/groups/{groupId}/members/{userId}).
public sealed record GetCompanyGroupMemberQuery(Guid GroupId, Guid UserId) : IRequest<Result<GroupMemberDetailsDto>>;

public sealed class GetCompanyGroupMemberQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetCompanyGroupMemberQuery, Result<GroupMemberDetailsDto>>
{
    public async Task<Result<GroupMemberDetailsDto>> Handle(GetCompanyGroupMemberQuery request, CancellationToken cancellationToken)
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

        var card = await context.GetMemberDetailsAsync(group, request.UserId, userDirectory, cancellationToken);
        return card is null ? GroupErrors.MemberNotFound : card;
    }
}
