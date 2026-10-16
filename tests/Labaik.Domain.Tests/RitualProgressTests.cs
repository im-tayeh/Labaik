using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals;
using Labaik.Domain.Rituals.Events;

namespace Labaik.Domain.Tests;

public class RitualProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static RitualProgress Start(int firstOrder = 1) =>
        RitualProgress.Start(Guid.NewGuid(), PilgrimageType.Umrah, firstOrder, Now).Value;

    [Fact]
    public void Start_validates_input()
    {
        Assert.Equal("Ritual.UserRequired", RitualProgress.Start(Guid.Empty, PilgrimageType.Umrah, 1, Now).Error!.Code);
        Assert.Equal("Ritual.InvalidOrder", RitualProgress.Start(Guid.NewGuid(), PilgrimageType.Umrah, 0, Now).Error!.Code);
    }

    [Fact]
    public void Completing_the_current_step_advances_to_the_next()
    {
        var progress = Start();

        Assert.True(progress.CompleteStep(1, nextOrder: 2, Now).IsSuccess);

        Assert.Equal(2, progress.CurrentOrder);
        Assert.False(progress.IsCompleted);
        Assert.Single(progress.CompletedSteps);
        Assert.IsType<RitualStepCompleted>(Assert.Single(progress.DomainEvents));
    }

    [Fact]
    public void Steps_must_be_completed_in_order()
    {
        var progress = Start();

        var result = progress.CompleteStep(3, nextOrder: 4, Now);

        Assert.Equal("Ritual.StepOutOfOrder", result.Error!.Code);
        Assert.Empty(progress.CompletedSteps);
    }

    [Fact]
    public void Completing_the_last_step_finishes_the_ritual()
    {
        var progress = Start();
        progress.CompleteStep(1, nextOrder: 2, Now);

        Assert.True(progress.CompleteStep(2, nextOrder: null, Now).IsSuccess);

        Assert.True(progress.IsCompleted);
        Assert.Equal(Now, progress.CompletedAtUtc);
        Assert.Contains(progress.DomainEvents, e => e is RitualCompleted);
        Assert.Equal("Ritual.AlreadyCompleted", progress.CompleteStep(2, null, Now).Error!.Code);
    }

    [Fact]
    public void Next_order_must_be_after_the_completed_step()
    {
        var progress = Start(firstOrder: 5);

        Assert.Equal("Ritual.InvalidOrder", progress.CompleteStep(5, nextOrder: 3, Now).Error!.Code);
    }

    [Fact]
    public void Reset_clears_progress()
    {
        var progress = Start();
        progress.CompleteStep(1, nextOrder: null, Now);

        Assert.True(progress.Reset(1, Now.AddHours(1)).IsSuccess);

        Assert.False(progress.IsCompleted);
        Assert.Equal(1, progress.CurrentOrder);
        Assert.Empty(progress.CompletedSteps);
        Assert.Equal(Now.AddHours(1), progress.StartedAtUtc);
    }
}
