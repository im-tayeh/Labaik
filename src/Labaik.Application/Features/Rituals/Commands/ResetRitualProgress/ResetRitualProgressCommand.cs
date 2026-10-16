using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals;
using MediatR;

namespace Labaik.Application.Features.Rituals.Commands.ResetRitualProgress;

public sealed record ResetRitualProgressCommand(PilgrimageType PilgrimageType) : IRequest<Result<RitualProgressDto>>;

public sealed class ResetRitualProgressCommandValidator : AbstractValidator<ResetRitualProgressCommand>
{
    public ResetRitualProgressCommandValidator()
        => RuleFor(x => x.PilgrimageType).IsInEnum();
}

public sealed class ResetRitualProgressCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<ResetRitualProgressCommand, Result<RitualProgressDto>>
{
    public async Task<Result<RitualProgressDto>> Handle(ResetRitualProgressCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var steps = await context.GetPublishedStepsAsync(request.PilgrimageType, cancellationToken);
        if (steps.Count == 0)
        {
            return RitualErrors.NoStepsAvailable;
        }

        var now = timeProvider.GetUtcNow();

        var load = await context.GetOrStartProgressAsync(userId, request.PilgrimageType, steps, now, cancellationToken);
        if (load.IsFailure)
        {
            return load.Error!;
        }

        var progress = load.Value;

        var reset = progress.Reset(steps[0].Order, now);
        if (reset.IsFailure)
        {
            return reset.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);

        return progress.ToDto(steps);
    }
}
