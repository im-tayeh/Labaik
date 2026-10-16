using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals;
using MediatR;

namespace Labaik.Application.Features.Rituals.Commands.CompleteRitualStep;

// StepOrder must be the pilgrim's current step; the client states it explicitly so retries are safe.
public sealed record CompleteRitualStepCommand(PilgrimageType PilgrimageType, int StepOrder) : IRequest<Result<RitualProgressDto>>;

public sealed class CompleteRitualStepCommandValidator : AbstractValidator<CompleteRitualStepCommand>
{
    public CompleteRitualStepCommandValidator()
    {
        RuleFor(x => x.PilgrimageType).IsInEnum();
        RuleFor(x => x.StepOrder).GreaterThan(0);
    }
}

public sealed class CompleteRitualStepCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<CompleteRitualStepCommand, Result<RitualProgressDto>>
{
    public async Task<Result<RitualProgressDto>> Handle(CompleteRitualStepCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var steps = await context.GetPublishedStepsAsync(request.PilgrimageType, cancellationToken);
        if (steps.All(s => s.Order != request.StepOrder))
        {
            return RitualErrors.StepNotFound;
        }

        var now = timeProvider.GetUtcNow();

        var load = await context.GetOrStartProgressAsync(userId, request.PilgrimageType, steps, now, cancellationToken);
        if (load.IsFailure)
        {
            return load.Error!;
        }

        var progress = load.Value;
        var nextOrder = steps.FirstOrDefault(s => s.Order > request.StepOrder)?.Order;

        var complete = progress.CompleteStep(request.StepOrder, nextOrder, now);
        if (complete.IsFailure)
        {
            return complete.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);

        return progress.ToDto(steps);
    }
}
