using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetGatheringPoint;

// Members only. Value is null when the supervisor has not set a point yet.
public sealed record GetGatheringPointQuery(Guid GroupId) : IRequest<Result<GatheringPointDto?>>;

public sealed class GetGatheringPointQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetGatheringPointQuery, Result<GatheringPointDto?>>
{
    public async Task<Result<GatheringPointDto?>> Handle(GetGatheringPointQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.Groups
            .Include(g => g.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        if (group is null || !group.IsMember(userId))
        {
            return GroupErrors.NotFound;
        }

        return Result.Success(group.GatheringPoint?.ToDto());
    }
}
