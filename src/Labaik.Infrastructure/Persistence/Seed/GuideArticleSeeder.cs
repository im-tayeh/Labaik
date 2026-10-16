using System.Text.Json;
using System.Text.Json.Serialization;
using Labaik.Domain.Guides;
using Labaik.Domain.Guides.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Persistence.Seed;

// Syncs guide articles from JSON seed files (idempotent; runs on every startup).
// Articles are matched by (Category, Order): new ones are inserted, existing ones updated,
// and articles no longer present in the file are unpublished (never deleted).
public static class GuideArticleSeeder
{
    private static readonly string[] SeedFiles = ["ihram-preparation.json"];

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
                logger.LogWarning("Guide seed file not found at {Path}; skipping.", path);
                continue;
            }

            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer.DeserializeAsync<GuideSeedFile>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Guide seed file '{fileName}' is empty.");

            await SyncAsync(context, file, fileName, cancellationToken);

            var changes = await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Guide seed {File} ({Category}) synced: {Count} articles, {Changes} row(s) changed.",
                fileName, file.Category, file.Articles.Count, changes);
        }
    }

    private static async Task SyncAsync(
        AppDbContext context, GuideSeedFile file, string fileName, CancellationToken cancellationToken)
    {
        var duplicate = file.Articles.GroupBy(a => a.Order).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Guide seed file '{fileName}' has duplicate order {duplicate.Key}.");
        }

        var existing = await context.GuideArticles
            .Where(a => a.Category == file.Category)
            .ToDictionaryAsync(a => a.Order, cancellationToken);

        foreach (var seed in file.Articles)
        {
            if (existing.Remove(seed.Order, out var article))
            {
                var update = article.UpdateContent(seed.Title, seed.Body, seed.Reference, seed.IsPublished);
                if (update.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Invalid guide seed article {seed.Order} in '{fileName}': {update.Error!.Description}");
                }

                continue;
            }

            var create = GuideArticle.Create(file.Category, seed.Order, seed.Title, seed.Body, seed.Reference, seed.IsPublished);
            if (create.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Invalid guide seed article {seed.Order} in '{fileName}': {create.Error!.Description}");
            }

            context.GuideArticles.Add(create.Value);
        }

        foreach (var removed in existing.Values)
        {
            removed.Unpublish();
        }
    }

    private sealed record GuideSeedFile(GuideCategory Category, List<GuideSeedArticle> Articles);

    private sealed record GuideSeedArticle(int Order, string Title, string Body, string? Reference, bool IsPublished);
}
