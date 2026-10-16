using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Locations;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Locations.Common;

internal static class LocationDataAccess
{
    // Loads the user's tracked share, or creates (and adds, unsaved) one with sharing off.
    public static async Task<Result<LocationShare>> GetOrCreateShareAsync(
        this IAppDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var share = await context.LocationShares.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (share is not null)
        {
            return share;
        }

        var create = LocationShare.Create(userId);
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.LocationShares.Add(create.Value);
        return create.Value;
    }

    public static async Task<List<Guid>> GetGroupIdsOfUserAsync(
        this IAppDbContext context, Guid userId, CancellationToken cancellationToken)
        => await context.Groups
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

    public static MyLocationSharingDto ToMyDto(this LocationShare s, DateTimeOffset nowUtc) => new(
        s.IsSharing, s.Latitude, s.Longitude, s.AccuracyMeters, s.UpdatedAtUtc, s.IsStale(nowUtc));
}
