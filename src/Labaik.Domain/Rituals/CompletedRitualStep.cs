using Labaik.Domain.Common;

namespace Labaik.Domain.Rituals;

public sealed class CompletedRitualStep : Entity
{
    public int StepOrder { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    private CompletedRitualStep() { } // EF

    internal CompletedRitualStep(int stepOrder, DateTimeOffset completedAtUtc)
    {
        StepOrder = stepOrder;
        CompletedAtUtc = completedAtUtc;
    }
}
