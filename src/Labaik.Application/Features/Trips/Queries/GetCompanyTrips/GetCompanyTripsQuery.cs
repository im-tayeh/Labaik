using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Queries.GetCompanyTrips;

// The current company's trips, soonest departure first.
public sealed record GetCompanyTripsQuery : IRequest<Result<IReadOnlyList<TripSummaryDto>>>;

public sealed class GetCompanyTripsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetCompanyTripsQuery, Result<IReadOnlyList<TripSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<TripSummaryDto>>> Handle(GetCompanyTripsQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var trips = await context.Trips
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId.Value)
            .OrderBy(t => t.DepartureDate)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var today = timeProvider.TodayUtc();
        return trips.Select(t => t.ToSummary(today)).ToList();
    }
}
