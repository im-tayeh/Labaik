using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys;
using Labaik.Domain.Journeys.Enums;
using MediatR;

namespace Labaik.Application.Features.Journeys.Commands.CreateJourney;

public sealed record CreateJourneyArrival(TransportMode Mode, string EntryPoint);

public sealed record CreateJourneyStay(string CityName, AccommodationType AccommodationType, string PlaceName, DateTimeOffset ArrivalAtUtc);

public sealed record CreateJourneyCommand(
    PilgrimageType PilgrimageType,
    bool IsFirstTime,
    TravelParty TravelParty,
    CreateJourneyArrival Arrival,
    List<CreateJourneyStay> Stays) : IRequest<Result<Guid>>;

public sealed class CreateJourneyCommandValidator : AbstractValidator<CreateJourneyCommand>
{
    public CreateJourneyCommandValidator()
    {
        RuleFor(x => x.PilgrimageType).IsInEnum();
        RuleFor(x => x.TravelParty).IsInEnum();
        RuleFor(x => x.Arrival).NotNull();
        RuleFor(x => x.Arrival.Mode).IsInEnum();
        RuleFor(x => x.Arrival.EntryPoint).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Stays).NotEmpty();
        RuleForEach(x => x.Stays).ChildRules(s =>
        {
            s.RuleFor(i => i.CityName).NotEmpty().MaximumLength(100);
            s.RuleFor(i => i.AccommodationType).IsInEnum();
            s.RuleFor(i => i.PlaceName).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class CreateJourneyCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<CreateJourneyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateJourneyCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        // Build the whole aggregate in memory, then save once (all inserts, no concurrency issues).
        var create = Journey.Create(userId, request.PilgrimageType, request.IsFirstTime, request.TravelParty);
        if (create.IsFailure)
        {
            return create.Error!;
        }

        var journey = create.Value;

        var arrival = journey.SetArrivalPlan(request.Arrival.Mode, request.Arrival.EntryPoint);
        if (arrival.IsFailure)
        {
            return arrival.Error!;
        }

        foreach (var s in request.Stays)
        {
            var add = journey.AddStay(s.CityName, s.AccommodationType, s.PlaceName, s.ArrivalAtUtc);
            if (add.IsFailure)
            {
                return add.Error!;
            }
        }

        var ready = journey.MarkReady();
        if (ready.IsFailure)
        {
            return ready.Error!;
        }

        context.Journeys.Add(journey);
        await context.SaveChangesAsync(cancellationToken); // single save

        return journey.Id;
    }
}