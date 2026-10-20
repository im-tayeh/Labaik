using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals.Enums;

namespace Labaik.Application.Features.Rituals.Dtos;

public sealed record RitualStepDto(
    Guid Id,
    PilgrimageType PilgrimageType,
    int Order,
    RitualPhase Phase,
    RitualStepKind Kind,
    string Title,
    string Body,
    string? Reference);

public sealed record CompletedRitualStepDto(int StepOrder, DateTimeOffset CompletedAtUtc);

public sealed record RitualProgressDto(
    Guid Id,
    PilgrimageType PilgrimageType,
    int CurrentOrder,
    int TotalSteps,
    int CompletedCount,
    bool IsCompleted,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    RitualStepDto? CurrentStep, // null once all steps are completed
    IReadOnlyList<CompletedRitualStepDto> CompletedSteps,
    string? CurrentStepKey = null,
    int? TawafRound = null,
    int? SaeeLap = null,
    IhramState? IhramState = null,
    DateTimeOffset? ClientUpdatedAtUtc = null);
