using Labaik.Domain.Locations;
using Labaik.Domain.Locations.Events;

namespace Labaik.Domain.Tests;

public class LocationShareTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static LocationShare Sharing()
    {
        var share = LocationShare.Create(Guid.NewGuid()).Value;
        share.StartSharing();
        return share;
    }

    [Fact]
    public void Sharing_is_off_by_default_and_positions_are_rejected()
    {
        var share = LocationShare.Create(Guid.NewGuid()).Value;

        Assert.False(share.IsSharing);
        Assert.Equal("Location.SharingDisabled", share.UpdatePosition(21.4, 39.8, 5, Now).Error!.Code);
    }

    [Fact]
    public void UpdatePosition_stores_last_position_and_raises_event()
    {
        var share = Sharing();

        Assert.True(share.UpdatePosition(21.4225, 39.8262, 5, Now).IsSuccess);

        Assert.Equal(21.4225, share.Latitude);
        Assert.Equal(Now, share.UpdatedAtUtc);
        Assert.IsType<MemberLocationUpdated>(Assert.Single(share.DomainEvents));
    }

    [Theory]
    [InlineData(91, 0, null, "Location.InvalidLatitude")]
    [InlineData(-91, 0, null, "Location.InvalidLatitude")]
    [InlineData(0, 181, null, "Location.InvalidLongitude")]
    [InlineData(0, 0, -1.0, "Location.InvalidAccuracy")]
    [InlineData(double.NaN, 0, null, "Location.InvalidLatitude")]
    public void UpdatePosition_validates_coordinates(double lat, double lng, double? accuracy, string expectedError)
    {
        Assert.Equal(expectedError, Sharing().UpdatePosition(lat, lng, accuracy, Now).Error!.Code);
    }

    [Fact]
    public void StopSharing_erases_the_position()
    {
        var share = Sharing();
        share.UpdatePosition(21.4, 39.8, 5, Now);
        share.ClearDomainEvents();

        share.StopSharing();

        Assert.False(share.IsSharing);
        Assert.False(share.HasPosition);
        Assert.Null(share.UpdatedAtUtc);
        Assert.IsType<MemberStoppedSharingLocation>(Assert.Single(share.DomainEvents));
    }

    [Fact]
    public void StopSharing_when_already_off_raises_nothing()
    {
        var share = LocationShare.Create(Guid.NewGuid()).Value;

        share.StopSharing();

        Assert.Empty(share.DomainEvents);
    }

    [Fact]
    public void Position_becomes_stale_after_fifteen_minutes()
    {
        var share = Sharing();
        share.UpdatePosition(21.4, 39.8, null, Now);

        Assert.False(share.IsStale(Now.AddMinutes(15)));
        Assert.True(share.IsStale(Now.AddMinutes(16)));
    }
}
