using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys;
using Labaik.Domain.Journeys.Enums;
using MediatR;

namespace Labaik.Application.Features.Journeys.Commands.CreateJourney;

public sealed record CreateJourneyCommand(PilgrimageType PilgrimageType, bool IsFirstTime, TravelParty TravelParty)
    : IRequest<Result<Guid>>;

public sealed class CreateJourneyCommandValidator : AbstractValidator<CreateJourneyCommand>
{
    public CreateJourneyCommandValidator()
    {
        RuleFor(x => x.PilgrimageType).IsInEnum();
        RuleFor(x => x.TravelParty).IsInEnum();
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

        var result = Journey.Create(userId, request.PilgrimageType, request.IsFirstTime, request.TravelParty);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        context.Journeys.Add(result.Value);
        await context.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}