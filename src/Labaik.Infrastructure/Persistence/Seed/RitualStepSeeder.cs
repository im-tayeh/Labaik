using System.Text.Json;
using System.Text.Json.Serialization;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Rituals;
using Labaik.Domain.Rituals.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Persistence.Seed;

// Syncs ritual guide content from JSON seed files into the database (idempotent; runs on every startup).
// Steps are matched by (PilgrimageType, Order): new ones are inserted, existing ones updated,
// and steps no longer present in the file are unpublished (never deleted, progress may reference them).
public static class RitualStepSeeder
{
    private static readonly string[] SeedFiles = ["umrah-steps.json"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        foreach (var fileName in SeedFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Persistence", "Seed", fileName);
            if (!File.Exists(path))
            {
                logger.LogWarning("Ritual seed file not found at {Path}; skipping.", path);
                continue;
            }

            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer.DeserializeAsync<RitualSeedFile>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Ritual seed file '{fileName}' is empty.");

            await SyncAsync(context, file, fileName, cancellationToken);

            var changes = await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Ritual seed {File} ({Type}) synced: {Count} steps, {Changes} row(s) changed.",
                fileName, file.PilgrimageType, file.Steps.Count, changes);
        }
    }

    private static async Task SyncAsync(
        AppDbContext context, RitualSeedFile file, string fileName, CancellationToken cancellationToken)
    {
        var duplicate = file.Steps.GroupBy(s => s.Order).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Ritual seed file '{fileName}' has duplicate order {duplicate.Key}.");
        }

        var existing = await context.RitualSteps
            .Where(s => s.PilgrimageType == file.PilgrimageType)
            .ToDictionaryAsync(s => s.Order, cancellationToken);

        foreach (var seed in file.Steps)
        {
            if (existing.Remove(seed.Order, out var step))
            {
                var update = step.UpdateContent(seed.Phase, seed.Kind, seed.Title, seed.Body, seed.Reference, seed.IsPublished);
                if (update.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Invalid ritual seed step {seed.Order} in '{fileName}': {update.Error!.Description}");
                }

                continue;
            }

            var create = RitualStep.Create(
                file.PilgrimageType, seed.Order, seed.Phase, seed.Kind, seed.Title, seed.Body, seed.Reference, seed.IsPublished);
            if (create.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Invalid ritual seed step {seed.Order} in '{fileName}': {create.Error!.Description}");
            }

            context.RitualSteps.Add(create.Value);
        }

        foreach (var removed in existing.Values)
        {
            removed.Unpublish();
        }
    }

    private sealed record RitualSeedFile(PilgrimageType PilgrimageType, List<RitualSeedStep> Steps);

    private sealed record RitualSeedStep(
        int Order, RitualPhase Phase, RitualStepKind Kind, string Title, string Body, string? Reference, bool IsPublished);
}
