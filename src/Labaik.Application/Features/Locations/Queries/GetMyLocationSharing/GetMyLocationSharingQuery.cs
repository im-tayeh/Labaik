using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Common;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Locations.Queries.GetMyLocationSharing;

public sealed record GetMyLocationSharingQuery : IRequest<Result<MyLocationSharingDto>>;

public sealed class GetMyLocationSharingQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetMyLocationSharingQuery, Result<MyLocationSharingDto>>
{
    public async Task<Result<MyLocationSharingDto>> Handle(GetMyLocationSharingQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var share = await context.LocationShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        // Never opted in yet -> sharing off, no position (nothing is created by a read).
        return share is null
            ? new MyLocationSharingDto(false, null, null, null, null, IsStale: true)
            : share.ToMyDto(timeProvider.GetUtcNow());
    }
}
