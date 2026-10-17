using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips;
using MediatR;

namespace Labaik.Application.Features.Trips.Commands.CreateTrip;

public sealed record CreateTripCommand(
    string Name,
    PilgrimageType PilgrimageType,
    List<string> Destinations,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    string DeparturePlace,
    int ExpectedPilgrims,
    string? Notes) : IRequest<Result<TripDto>>, ITripFields;

public sealed class CreateTripCommandValidator : TripFieldsValidator<CreateTripCommand>;

public sealed class CreateTripCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<CreateTripCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var create = Trip.Create(companyId.Value, request.ToDetails(), timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.Trips.Add(create.Value);
        await context.SaveChangesAsync(cancellationToken);

        return create.Value.ToDto(timeProvider.TodayUtc());
    }
}
