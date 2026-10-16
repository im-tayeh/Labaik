using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Journeys;

// Input for Journey.ReplaceStays (the Stay entity itself is created only by the aggregate).
public sealed record StayDetails(string CityName, AccommodationType AccommodationType, string PlaceName, DateTimeOffset ArrivalAtUtc);
