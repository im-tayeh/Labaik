using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using MediatR;

namespace Labaik.Application.Features.Rituals.Queries.GetRitualSteps;

public sealed record GetRitualStepsQuery(PilgrimageType PilgrimageType) : IRequest<Result<IReadOnlyList<RitualStepDto>>>;

public sealed class GetRitualStepsQueryValidator : AbstractValidator<GetRitualStepsQuery>
{
    public GetRitualStepsQueryValidator()
        => RuleFor(x => x.PilgrimageType).IsInEnum();
}

public sealed class GetRitualStepsQueryHandler(
    IAppDbContext context) : IRequestHandler<GetRitualStepsQuery, Result<IReadOnlyList<RitualStepDto>>>
{
    public async Task<Result<IReadOnlyList<RitualStepDto>>> Handle(GetRitualStepsQuery request, CancellationToken cancellationToken)
    {
        var steps = await context.GetPublishedStepsAsync(request.PilgrimageType, cancellationToken);
        return steps.Select(s => s.ToDto()).ToList();
    }
}
