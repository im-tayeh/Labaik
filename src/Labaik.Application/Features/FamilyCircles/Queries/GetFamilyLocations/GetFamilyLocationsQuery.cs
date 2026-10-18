using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.FamilyCircles.Queries.GetFamilyLocations;

// "View location": every member of the circle, with a position only for those who show it here.
public sealed record GetFamilyLocationsQuery(Guid CircleId) : IRequest<Result<IReadOnlyList<FamilyMemberLocationDto>>>;

public sealed class GetFamilyLocationsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<GetFamilyLocationsQuery, Result<IReadOnlyList<FamilyMemberLocationDto>>>
{
    public async Task<Result<IReadOnlyList<FamilyMemberLocationDto>>> Handle(
        GetFamilyLocationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var circle = await context.GetCircleForMemberAsync(request.CircleId, userId, cancellationToken);
        if (circle is null)
        {
            return FamilyCircleErrors.NotFound;
        }

        var ids = circle.Members.Select(m => m.UserId).ToList();
        var shares = await context.LocationShares.AsNoTracking()
            .Where(s => ids.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, cancellationToken);
        var names = await userDirectory.GetFullNamesAsync(ids, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return circle.Members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(m =>
            {
                var share = shares.GetValueOrDefault(m.UserId);
                var sharing = m.IsSharingLocation && share is { IsSharing: true, HasPosition: true };
                return new FamilyMemberLocationDto(
                    m.UserId,
                    names.GetValueOrDefault(m.UserId),
                    sharing,
                    sharing ? share!.Latitude : null,
                    sharing ? share!.Longitude : null,
                    sharing ? share!.AccuracyMeters : null,
                    sharing ? share!.UpdatedAtUtc : null,
                    !sharing || share!.IsStale(now));
            })
            .ToList();
    }
}
