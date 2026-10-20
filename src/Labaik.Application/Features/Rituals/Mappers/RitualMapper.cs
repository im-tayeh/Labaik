using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Domain.Rituals;

namespace Labaik.Application.Features.Rituals.Mappers;

public static class RitualMapper
{
    public static RitualStepDto ToDto(this RitualStep s) => new(
        s.Id, s.PilgrimageType, s.Order, s.Phase, s.Kind, s.Title, s.Body, s.Reference);

    // publishedSteps: the ordered published steps of the progress' pilgrimage type.
    public static RitualProgressDto ToDto(this RitualProgress p, IReadOnlyList<RitualStep> publishedSteps) => new(
        p.Id, p.PilgrimageType, p.CurrentOrder, publishedSteps.Count, p.CompletedSteps.Count, p.IsCompleted,
        p.StartedAtUtc, p.CompletedAtUtc,
        p.IsCompleted ? null : publishedSteps.FirstOrDefault(s => s.Order == p.CurrentOrder)?.ToDto(),
        p.CompletedSteps
            .OrderBy(c => c.StepOrder)
            .Select(c => new CompletedRitualStepDto(c.StepOrder, c.CompletedAtUtc))
            .ToList(),
        p.CurrentStepKey, p.TawafRound, p.SaeeLap, p.IhramState, p.ClientUpdatedAtUtc);
}
