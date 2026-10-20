using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals.Events;
using Labaik.Domain.Rituals.Enums;

namespace Labaik.Domain.Rituals;

// A pilgrim's walk through the ritual steps of one pilgrimage type. Steps complete strictly in order.
public sealed class RitualProgress : AggregateRoot
{
    public Guid UserId { get; private set; }
    public PilgrimageType PilgrimageType { get; private set; }
    public int CurrentOrder { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CurrentStepKey { get; private set; }
    public int? TawafRound { get; private set; }
    public int? SaeeLap { get; private set; }
    public IhramState? IhramState { get; private set; }
    public DateTimeOffset? ClientUpdatedAtUtc { get; private set; }

    private readonly List<CompletedRitualStep> _completedSteps = [];
    public IReadOnlyList<CompletedRitualStep> CompletedSteps => _completedSteps.AsReadOnly();

    public bool IsCompleted => CompletedAtUtc is not null;

    private RitualProgress() { } // EF

    private RitualProgress(Guid userId, PilgrimageType type, int firstOrder, DateTimeOffset startedAtUtc)
    {
        UserId = userId;
        PilgrimageType = type;
        CurrentOrder = firstOrder;
        StartedAtUtc = startedAtUtc;
    }

    // Factory: firstOrder is the order of the first published step (supplied by the app layer).
    public static Result<RitualProgress> Start(
        Guid userId, PilgrimageType type, int firstOrder, DateTimeOffset startedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return RitualErrors.UserRequired;
        }

        if (firstOrder <= 0)
        {
            return RitualErrors.InvalidOrder;
        }

        return new RitualProgress(userId, type, firstOrder, startedAtUtc);
    }

    // nextOrder is the next published step after stepOrder, or null when stepOrder is the last one.
    public Result CompleteStep(int stepOrder, int? nextOrder, DateTimeOffset completedAtUtc)
    {
        if (IsCompleted)
        {
            return Result.Failure(RitualErrors.AlreadyCompleted);
        }

        if (stepOrder != CurrentOrder)
        {
            return Result.Failure(RitualErrors.StepOutOfOrder(CurrentOrder, stepOrder));
        }

        if (nextOrder is { } next && next <= stepOrder)
        {
            return Result.Failure(RitualErrors.InvalidOrder);
        }

        _completedSteps.Add(new CompletedRitualStep(stepOrder, completedAtUtc));
        Raise(new RitualStepCompleted(Id, UserId, stepOrder));

        if (nextOrder is { } following)
        {
            CurrentOrder = following;
        }
        else
        {
            CompletedAtUtc = completedAtUtc;
            Raise(new RitualCompleted(Id, UserId, PilgrimageType));
        }

        return Result.Success();
    }

    public Result Reset(int firstOrder, DateTimeOffset restartedAtUtc)
    {
        if (firstOrder <= 0)
        {
            return Result.Failure(RitualErrors.InvalidOrder);
        }

        _completedSteps.Clear();
        CurrentOrder = firstOrder;
        StartedAtUtc = restartedAtUtc;
        CompletedAtUtc = null;
        return Result.Success();
    }

    // Last write wins by client timestamp; stale snapshots are ignored.
    public Result SaveSnapshot(
        string? stepKey, int? round, int? saeeLap, IhramState? ihramState, DateTimeOffset updatedAtUtc)
    {
        if (round is < 1 or > 7 || saeeLap is < 1 or > 7) return Result.Failure(RitualErrors.InvalidRound);
        if (ClientUpdatedAtUtc is { } last && updatedAtUtc <= last) return Result.Success();

        CurrentStepKey = string.IsNullOrWhiteSpace(stepKey) ? null : stepKey.Trim();
        TawafRound = round;
        SaeeLap = saeeLap;
        IhramState = ihramState;
        ClientUpdatedAtUtc = updatedAtUtc;
        return Result.Success();
    }
}
