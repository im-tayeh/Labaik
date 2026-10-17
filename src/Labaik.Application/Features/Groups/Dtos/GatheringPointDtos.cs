using Labaik.Domain.Groups.ValueObjects;

namespace Labaik.Application.Features.Groups.Dtos;

public sealed record GatheringPointDto(
    string Address,
    double Latitude,
    double Longitude,
    DateTimeOffset? GatheringTimeUtc,
    DateTimeOffset UpdatedAtUtc);

public static class GatheringPointMapper
{
    public static GatheringPointDto ToDto(this GatheringPoint p) =>
        new(p.Address, p.Latitude, p.Longitude, p.GatheringTimeUtc, p.UpdatedAtUtc);
}
