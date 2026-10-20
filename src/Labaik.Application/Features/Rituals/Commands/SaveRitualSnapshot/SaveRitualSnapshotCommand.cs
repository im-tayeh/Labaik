using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Rituals.Commands.SaveRitualSnapshot;

public sealed record SaveRitualSnapshotCommand(
    string? StepKey, int? Round, int? SaeeLap, IhramState? IhramState, DateTimeOffset UpdatedAtUtc)
    : IRequest<Result<RitualProgressDto>>;

public sealed class SaveRitualSnapshotCommandValidator : AbstractValidator<SaveRitualSnapshotCommand>
{
    public SaveRitualSnapshotCommandValidator()
    {
        RuleFor(x => x.StepKey).MaximumLength(100);
        RuleFor(x => x.Round).InclusiveBetween(1, 7).When(x => x.Round.HasValue);
        RuleFor(x => x.SaeeLap).InclusiveBetween(1, 7).When(x => x.SaeeLap.HasValue);
        RuleFor(x => x.IhramState).IsInEnum().When(x => x.IhramState.HasValue);
        RuleFor(x => x.UpdatedAtUtc).NotEmpty();
    }
}

public sealed class SaveRitualSnapshotCommandHandler(
    IAppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<SaveRitualSnapshotCommand, Result<RitualProgressDto>>
{
    public async Task<Result<RitualProgressDto>> Handle(
        SaveRitualSnapshotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId) return Error.Unauthorized("Auth.Required", "Authentication is required.");

        var type = PilgrimageType.Umrah;
        var steps = await context.GetPublishedStepsAsync(type, cancellationToken);
        var loaded = await context.GetOrStartProgressAsync(
            userId, type, steps, timeProvider.GetUtcNow(), cancellationToken);
        if (loaded.IsFailure) return loaded.Error!;

        var saved = loaded.Value.SaveSnapshot(
            request.StepKey, request.Round, request.SaeeLap, request.IhramState, request.UpdatedAtUtc);
        if (saved.IsFailure) return saved.Error!;
        await context.SaveChangesAsync(cancellationToken);
        return loaded.Value.ToDto(steps);
    }
}
