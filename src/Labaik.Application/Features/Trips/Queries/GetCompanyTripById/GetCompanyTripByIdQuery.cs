using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Queries.GetCompanyTripById;

public sealed record GetCompanyTripByIdQuery(Guid TripId) : IRequest<Result<TripDto>>;

public sealed class GetCompanyTripByIdQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetCompanyTripByIdQuery, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(GetCompanyTripByIdQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var trip = await context.Trips
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TripId && t.CompanyId == companyId.Value, cancellationToken);

        return trip is null ? TripErrors.NotFound : trip.ToDto(timeProvider.TodayUtc());
    }
}
