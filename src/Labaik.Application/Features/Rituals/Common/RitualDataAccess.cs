using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Rituals.Common;

internal static class RitualDataAccess
{
    public static async Task<List<RitualStep>> GetPublishedStepsAsync(
        this IAppDbContext context, PilgrimageType type, CancellationToken cancellationToken)
        => await context.RitualSteps
            .AsNoTracking()
            .Where(s => s.PilgrimageType == type && s.IsPublished)
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);

    // Loads the user's tracked progress, or starts (and adds, unsaved) a new one at the first step.
    public static async Task<Result<RitualProgress>> GetOrStartProgressAsync(
        this IAppDbContext context,
        Guid userId,
        PilgrimageType type,
        IReadOnlyList<RitualStep> publishedSteps,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var progress = await context.RitualProgresses
            .Include(p => p.CompletedSteps)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PilgrimageType == type, cancellationToken);

        if (progress is not null)
        {
            return progress;
        }

        if (publishedSteps.Count == 0)
        {
            return RitualErrors.NoStepsAvailable;
        }

        var start = RitualProgress.Start(userId, type, publishedSteps[0].Order, nowUtc);
        if (start.IsFailure)
        {
            return start.Error!;
        }

        context.RitualProgresses.Add(start.Value);
        return start.Value;
    }
}
