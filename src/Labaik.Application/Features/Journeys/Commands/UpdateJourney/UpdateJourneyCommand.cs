using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Journeys.Commands.CreateJourney;
using Labaik.Application.Features.Journeys.Dtos;
using Labaik.Application.Features.Journeys.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Commands.UpdateJourney;

// HTTP body for the edit screen; same arrival/stay shapes as onboarding. PilgrimageType is not editable.
public sealed record UpdateJourneyRequest(
    bool IsFirstTime,
    TravelParty TravelParty,
    CreateJourneyArrival Arrival,
    List<CreateJourneyStay> Stays);

public sealed record UpdateJourneyCommand(
    Guid JourneyId,
    bool IsFirstTime,
    TravelParty TravelParty,
    CreateJourneyArrival Arrival,
    List<CreateJourneyStay> Stays) : IRequest<Result<JourneyDto>>
{
    public static UpdateJourneyCommand From(Guid journeyId, UpdateJourneyRequest body)
        => new(journeyId, body.IsFirstTime, body.TravelParty, body.Arrival, body.Stays);
}

public sealed class UpdateJourneyCommandValidator : AbstractValidator<UpdateJourneyCommand>
{
    public UpdateJourneyCommandValidator()
    {
        RuleFor(x => x.JourneyId).NotEmpty();
        RuleFor(x => x.TravelParty).IsInEnum();
        RuleFor(x => x.Arrival).NotNull();
        RuleFor(x => x.Arrival.Mode).IsInEnum().When(x => x.Arrival is not null);
        RuleFor(x => x.Arrival.EntryPoint).NotEmpty().MaximumLength(200).When(x => x.Arrival is not null);
        RuleFor(x => x.Stays).NotEmpty();
        RuleForEach(x => x.Stays).ChildRules(s =>
        {
            s.RuleFor(i => i.CityName).NotEmpty().MaximumLength(100);
            s.RuleFor(i => i.AccommodationType).IsInEnum();
            s.RuleFor(i => i.PlaceName).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class UpdateJourneyCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<UpdateJourneyCommand, Result<JourneyDto>>
{
    public async Task<Result<JourneyDto>> Handle(UpdateJourneyCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var journey = await context.Journeys
            .Include(j => j.Stays)
            .FirstOrDefaultAsync(j => j.Id == request.JourneyId, cancellationToken);

        if (journey is null || journey.UserId != userId)
        {
            return JourneyErrors.NotFound;
        }

        var details = journey.UpdateDetails(request.IsFirstTime, request.TravelParty);
        if (details.IsFailure)
        {
            return details.Error!;
        }

        var arrival = journey.SetArrivalPlan(request.Arrival.Mode, request.Arrival.EntryPoint);
        if (arrival.IsFailure)
        {
            return arrival.Error!;
        }

        var stays = journey.ReplaceStays(request.Stays
            .Select(s => new StayDetails(s.CityName, s.AccommodationType, s.PlaceName, s.ArrivalAtUtc))
            .ToList());
        if (stays.IsFailure)
        {
            return stays.Error!;
        }

        await context.SaveChangesAsync(cancellationToken); // single save

        return journey.ToDto();
    }
}
