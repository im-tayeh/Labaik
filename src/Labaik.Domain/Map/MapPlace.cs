using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Map;

public enum MapPlaceType { MainGate = 1, SubGate = 2, Facility = 3 }

// Facility icon/category for type 3 (null for gates).
public enum FacilityKind { Restrooms = 1, Medical = 2, ZamzamWater = 3, LostAndFound = 4, WheelchairService = 5 }

// A point on the field-mode map (Haram gates and facilities). Content is seed data (Arabic names in JSON),
// matched by a stable Key; places dropped from the seed are unpublished, never deleted.
public sealed class MapPlace : AggregateRoot
{
    public const int KeyMaxLength = 50;
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int UrlMaxLength = 2048;

    public string Key { get; private set; } = string.Empty;
    public MapPlaceType Type { get; private set; }
    public FacilityKind? FacilityKind { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int? Number { get; private set; } // gate number, when it has one
    public string? Description { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string? ImageUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsPublished { get; private set; }

    private MapPlace() { } // EF

    private MapPlace(string key) => Key = key;

    public static Result<MapPlace> Create(string key, MapPlaceContent content)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > KeyMaxLength)
        {
            return MapErrors.InvalidKey;
        }

        var place = new MapPlace(key.Trim());
        var update = place.Update(content);
        return update.IsFailure ? update.Error! : place;
    }

    public Result Update(MapPlaceContent content)
    {
        if (string.IsNullOrWhiteSpace(content.Name) || content.Name.Trim().Length > NameMaxLength
            || content.Description is { Length: > DescriptionMaxLength } || content.ImageUrl is { Length: > UrlMaxLength })
        {
            return Result.Failure(MapErrors.InvalidContent);
        }

        if (!Geo.IsValid(content.Latitude, content.Longitude))
        {
            return Result.Failure(MapErrors.InvalidCoordinates);
        }

        if ((content.Type == MapPlaceType.Facility) != (content.FacilityKind is not null))
        {
            return Result.Failure(MapErrors.FacilityKindMismatch);
        }

        Type = content.Type;
        FacilityKind = content.FacilityKind;
        Name = content.Name.Trim();
        Number = content.Number;
        Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description.Trim();
        Latitude = content.Latitude;
        Longitude = content.Longitude;
        ImageUrl = string.IsNullOrWhiteSpace(content.ImageUrl) ? null : content.ImageUrl.Trim();
        SortOrder = content.SortOrder;
        IsPublished = content.IsPublished;
        return Result.Success();
    }

    public void Unpublish() => IsPublished = false;
}

public sealed record MapPlaceContent(
    MapPlaceType Type,
    FacilityKind? FacilityKind,
    string Name,
    int? Number,
    string? Description,
    double Latitude,
    double Longitude,
    string? ImageUrl,
    int SortOrder,
    bool IsPublished);
