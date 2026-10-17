namespace Labaik.Domain.Groups.ValueObjects;

// Where (and optionally when) the group gathers. Set by the group's supervisor; replaced as a whole.
public sealed record GatheringPoint(
    string Address,
    double Latitude,
    double Longitude,
    DateTimeOffset? GatheringTimeUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public const int AddressMaxLength = 300;
}
