using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Rituals.Queries.GetRitualProgress;

// Auto-starts: the first read creates the pilgrim's progress at the first published step.
public sealed record GetRitualProgressQuery(PilgrimageType PilgrimageType) : IRequest<Result<RitualProgressDto>>;

public sealed class GetRitualProgressQueryValidator : AbstractValidator<GetRitualProgressQuery>
{
    public GetRitualProgressQueryValidator()
        => RuleFor(x => x.PilgrimageType).IsInEnum();
}

public sealed class GetRitualProgressQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetRitualProgressQuery, Result<RitualProgressDto>>
{
    public async Task<Result<RitualProgressDto>> Handle(GetRitualProgressQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var steps = await context.GetPublishedStepsAsync(request.PilgrimageType, cancellationToken);

        var load = await context.GetOrStartProgressAsync(
            userId, request.PilgrimageType, steps, timeProvider.GetUtcNow(), cancellationToken);
        if (load.IsFailure)
        {
            return load.Error!;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken); // no-op unless just started
        }
        catch (DbUpdateException)
        {
            // A concurrent request started the same progress first (unique index); read theirs.
            var existing = await context.RitualProgresses
                .Include(p => p.CompletedSteps)
                .AsNoTracking()
                .FirstAsync(p => p.UserId == userId && p.PilgrimageType == request.PilgrimageType, cancellationToken);

            return existing.ToDto(steps);
        }

        return load.Value.ToDto(steps);
    }
}
