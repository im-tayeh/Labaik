using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals.Enums;

namespace Labaik.Domain.Rituals;

// Guide content for one ritual step. Seeded data (pending religious approval), not user data.
public sealed class RitualStep : AggregateRoot
{
    public PilgrimageType PilgrimageType { get; private set; }
    public int Order { get; private set; }
    public RitualPhase Phase { get; private set; }
    public RitualStepKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public bool IsPublished { get; private set; }

    private RitualStep() { } // EF

    private RitualStep(PilgrimageType type, int order)
    {
        PilgrimageType = type;
        Order = order;
    }

    public static Result<RitualStep> Create(
        PilgrimageType type, int order, RitualPhase phase, RitualStepKind kind,
        string title, string body, string? reference, bool isPublished)
    {
        if (order <= 0)
        {
            return RitualErrors.InvalidOrder;
        }

        var step = new RitualStep(type, order);
        var update = step.UpdateContent(phase, kind, title, body, reference, isPublished);
        return update.IsFailure ? update.Error! : step;
    }

    public Result UpdateContent(
        RitualPhase phase, RitualStepKind kind, string title, string body, string? reference, bool isPublished)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(RitualErrors.TitleRequired);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure(RitualErrors.BodyRequired);
        }

        Phase = phase;
        Kind = kind;
        Title = title.Trim();
        Body = body.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        IsPublished = isPublished;
        return Result.Success();
    }

    public void Unpublish() => IsPublished = false;
}
