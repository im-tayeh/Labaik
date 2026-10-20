using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Groups;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Common;

internal static class GroupSummaryBuilder
{
    public static async Task<GroupSummaryDto> BuildAsync(
        IAppDbContext context, IUserDirectory userDirectory, Group group, CancellationToken cancellationToken)
    {
        var leaderName = group.LeaderId is { } leaderId
            ? await userDirectory.GetFullNameAsync(leaderId, cancellationToken)
            : null;
        var leaderPhotoUrl = group.LeaderId is { } photoLeaderId
            ? await context.PilgrimProfiles.AsNoTracking()
                .Where(profile => profile.UserId == photoLeaderId)
                .Select(profile => profile.PhotoUrl)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var tripDate = group.TripId is { } tripId
            ? await context.Trips.AsNoTracking().Where(trip => trip.Id == tripId)
                .Select(trip => (DateOnly?)trip.DepartureDate).FirstOrDefaultAsync(cancellationToken)
            : null;
        var companyName = group.CompanyId is { } companyId
            ? await context.Companies.AsNoTracking().Where(company => company.Id == companyId)
                .Select(company => company.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new GroupSummaryDto(group.Id, group.Name, leaderName, group.MemberCount,
            tripDate, companyName, leaderPhotoUrl);
    }
}
