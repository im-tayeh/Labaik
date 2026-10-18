using Labaik.Domain.Map;
using Labaik.Domain.Map.Events;

namespace Labaik.Domain.Tests;

public class MapTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Distance_and_bearing_are_plausible_at_walking_scale()
    {
        // ~111 m due north per 0.001 degree of latitude.
        Assert.InRange(Geo.DistanceMeters(21.4200, 39.8260, 21.4210, 39.8260), 110, 112);
        Assert.InRange(Geo.BearingDegrees(21.4200, 39.8260, 21.4210, 39.8260), 0, 0.5);
        Assert.InRange(Geo.BearingDegrees(21.4200, 39.8260, 21.4200, 39.8270), 89.5, 90.5);
    }

    [Fact]
    public void Map_places_validate_facility_kind_and_coordinates()
    {
        MapPlaceContent Content(MapPlaceType type, FacilityKind? kind, double lat = 21.42) =>
            new(type, kind, "Name", null, null, lat, 39.82, null, 1, true);

        Assert.True(MapPlace.Create("gate-1", Content(MapPlaceType.MainGate, null)).IsSuccess);
        Assert.Equal("Map.FacilityKindMismatch", MapPlace.Create("f", Content(MapPlaceType.Facility, null)).Error!.Code);
        Assert.Equal("Map.FacilityKindMismatch", MapPlace.Create("g", Content(MapPlaceType.SubGate, FacilityKind.Medical)).Error!.Code);
        Assert.Equal("Map.InvalidCoordinates", MapPlace.Create("x", Content(MapPlaceType.MainGate, null, 91)).Error!.Code);
    }

    [Fact]
    public void Emergency_raises_event_and_resolves_once()
    {
        var alert = EmergencyAlert.Create(Guid.NewGuid(), 21.42, 39.82, 8, " help ", Now).Value;
        Assert.Equal("help", alert.Note);
        Assert.IsType<EmergencyReported>(Assert.Single(alert.DomainEvents));

        var resolver = Guid.NewGuid();
        alert.Resolve(resolver, Now);
        alert.Resolve(Guid.NewGuid(), Now.AddMinutes(5));
        Assert.Equal(EmergencyStatus.Resolved, alert.Status);
        Assert.Equal(resolver, alert.ResolvedBy);
        Assert.Equal("Map.InvalidCoordinates", EmergencyAlert.Create(Guid.NewGuid(), 0, 200, null, null, Now).Error!.Code);
    }
}
