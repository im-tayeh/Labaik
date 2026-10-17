using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Queries.GetGroupAttendance;

// Group supervisor only (members -> 403, non-members -> 404).
public sealed record GetGroupAttendanceQuery(Guid GroupId) : IRequest<Result<GroupAttendanceDto>>;

public sealed class GetGroupAttendanceQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetGroupAttendanceQuery, Result<GroupAttendanceDto>>
{
    public async Task<Result<GroupAttendanceDto>> Handle(GetGroupAttendanceQuery request, CancellationToken cancellationToken)
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

        return await group.ToAttendanceDtoAsync(userDirectory, cancellationToken);
    }
}
