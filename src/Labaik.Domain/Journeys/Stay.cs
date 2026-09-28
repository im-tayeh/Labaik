using Labaik.Domain.Common;
using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Journeys;

public sealed class Stay : Entity
{
    public string CityName { get; private set; }
    public AccommodationType AccommodationType { get; private set; }
    public string PlaceName { get; private set; } = string.Empty;
    public DateTimeOffset ArrivalAtUtc { get; private set; }

    private Stay() { }

    internal Stay(string city, AccommodationType type, string placeName, DateTimeOffset arrivalAtUtc)
    {
        CityName = city;
        AccommodationType = type;
        PlaceName = placeName;
        ArrivalAtUtc = arrivalAtUtc;
    }
}