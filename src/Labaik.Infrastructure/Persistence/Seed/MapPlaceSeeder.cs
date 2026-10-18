using System.Text.Json;
using System.Text.Json.Serialization;
using Labaik.Domain.Map;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Persistence.Seed;

// Syncs field-mode map places from haram-map.json (idempotent; runs on every startup).
// Places are matched by Key: new ones are inserted, existing ones updated, and places no longer
// in the file are unpublished (never deleted).
public static class MapPlaceSeeder
{
    private const string FileName = "haram-map.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Persistence", "Seed", FileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Map seed file not found at {Path}; skipping.", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<MapSeedFile>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Map seed file '{FileName}' is empty.");

        var duplicate = file.Places.GroupBy(p => p.Key).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Map seed file '{FileName}' has duplicate key '{duplicate.Key}'.");
        }

        var existing = await context.MapPlaces.ToDictionaryAsync(p => p.Key, cancellationToken);
        foreach (var (seed, index) in file.Places.Select((p, i) => (p, i)))
        {
            var content = new MapPlaceContent(
                seed.Type, seed.FacilityKind, seed.Name, seed.Number, seed.Description,
                seed.Latitude, seed.Longitude, seed.ImageUrl, SortOrder: index + 1, seed.IsPublished);

            if (existing.Remove(seed.Key, out var place))
            {
                var update = place.Update(content);
                if (update.IsFailure)
                {
                    throw new InvalidOperationException($"Invalid map place '{seed.Key}': {update.Error!.Description}");
                }

                continue;
            }

            var create = MapPlace.Create(seed.Key, content);
            if (create.IsFailure)
            {
                throw new InvalidOperationException($"Invalid map place '{seed.Key}': {create.Error!.Description}");
            }

            context.MapPlaces.Add(create.Value);
        }

        foreach (var removed in existing.Values)
        {
            removed.Unpublish();
        }

        var changes = await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Map seed {File} synced: {Count} places, {Changes} row(s) changed.",
            FileName, file.Places.Count, changes);
    }

    private sealed record MapSeedFile(List<MapSeedPlace> Places);

    private sealed record MapSeedPlace(
        string Key,
        MapPlaceType Type,
        FacilityKind? FacilityKind,
        string Name,
        int? Number,
        string? Description,
        double Latitude,
        double Longitude,
        string? ImageUrl,
        bool IsPublished = true);
}
