using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Common;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Locations.Queries.GetMyGroupIds;

// Used by the realtime hub to subscribe a connection to the user's groups.
public sealed record GetMyGroupIdsQuery : IRequest<Result<IReadOnlyList<Guid>>>;

public sealed class GetMyGroupIdsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyGroupIdsQuery, Result<IReadOnlyList<Guid>>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetMyGroupIdsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        return await context.GetGroupIdsOfUserAsync(userId, cancellationToken);
    }
}
