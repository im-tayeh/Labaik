using Labaik.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Common;

// Who may see or change a trip's shared data (e.g. its itinerary).
internal static class TripAccess
{
    // The company that owns the trip.
    public static Task<bool> IsTripCompanyOwnerAsync(
        this IAppDbContext context, Guid tripId, Guid userId, CancellationToken cancellationToken)
        => context.Trips.AnyAsync(
            t => t.Id == tripId && context.Companies.Any(c => c.Id == t.CompanyId && c.OwnerUserId == userId),
            cancellationToken);

    // Supervisor of at least one group under the trip.
    public static Task<bool> IsTripSupervisorAsync(
        this IAppDbContext context, Guid tripId, Guid userId, CancellationToken cancellationToken)
        => context.Groups.AnyAsync(g => g.TripId == tripId && g.LeaderId == userId, cancellationToken);

    // Member (pilgrim or supervisor) of at least one group under the trip.
    public static Task<bool> IsTripMemberAsync(
        this IAppDbContext context, Guid tripId, Guid userId, CancellationToken cancellationToken)
        => context.Groups.AnyAsync(g => g.TripId == tripId && g.Members.Any(m => m.UserId == userId), cancellationToken);
}
