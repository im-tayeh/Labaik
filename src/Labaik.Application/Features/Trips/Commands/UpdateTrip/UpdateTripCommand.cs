using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Commands.UpdateTrip;

// HTTP body: the same fields as create; the trip comes from the route.
public sealed record UpdateTripRequest(
    string Name,
    PilgrimageType PilgrimageType,
    List<string> Destinations,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    string DeparturePlace,
    int ExpectedPilgrims,
    string? Notes);

public sealed record UpdateTripCommand(
    Guid TripId,
    string Name,
    PilgrimageType PilgrimageType,
    List<string> Destinations,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    string DeparturePlace,
    int ExpectedPilgrims,
    string? Notes) : IRequest<Result<TripDto>>, ITripFields
{
    public static UpdateTripCommand From(Guid tripId, UpdateTripRequest b) => new(
        tripId, b.Name, b.PilgrimageType, b.Destinations, b.DepartureDate, b.ReturnDate, b.DeparturePlace, b.ExpectedPilgrims, b.Notes);
}

public sealed class UpdateTripCommandValidator : TripFieldsValidator<UpdateTripCommand>
{
    public UpdateTripCommandValidator() => RuleFor(x => x.TripId).NotEmpty();
}

public sealed class UpdateTripCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<UpdateTripCommand, Result<TripDto>>
{
    public async Task<Result<TripDto>> Handle(UpdateTripCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        // Another company's trip is reported as not found (never leak existence).
        var trip = await context.Trips
            .FirstOrDefaultAsync(t => t.Id == request.TripId && t.CompanyId == companyId.Value, cancellationToken);
        if (trip is null)
        {
            return TripErrors.NotFound;
        }

        var update = trip.Update(request.ToDetails());
        if (update.IsFailure)
        {
            return update.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return trip.ToDto(timeProvider.TodayUtc());
    }
}
