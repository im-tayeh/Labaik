using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Locations.Queries.GetGroupMemberLocations;

// "Where are my group members": every member, with a position only for those sharing it.
public sealed record GetGroupMemberLocationsQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<MemberLocationDto>>>;

public sealed class GetGroupMemberLocationsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<GetGroupMemberLocationsQuery, Result<IReadOnlyList<MemberLocationDto>>>
{
    public async Task<Result<IReadOnlyList<MemberLocationDto>>> Handle(GetGroupMemberLocationsQuery request, CancellationToken cancellationToken)
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

        var memberIds = group.Members.Select(m => m.UserId).ToList();

        var shares = await context.LocationShares
            .AsNoTracking()
            .Where(s => memberIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        var names = await userDirectory.GetFullNamesAsync(memberIds, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return group.Members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(m =>
            {
                var share = shares.GetValueOrDefault(m.UserId);
                var sharing = share is { IsSharing: true };
                return new MemberLocationDto(
                    m.UserId,
                    names.GetValueOrDefault(m.UserId),
                    m.Role,
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
