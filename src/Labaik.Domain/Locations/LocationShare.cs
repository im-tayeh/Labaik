using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Locations.Events;

namespace Labaik.Domain.Locations;

// A pilgrim's opt-in live location, visible to members of their groups.
// Only the last known position is kept (no history); stopping sharing erases it.
public sealed class LocationShare : AggregateRoot
{
    // A position older than this is shown as stale ("last seen").
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public Guid UserId { get; private set; }
    public bool IsSharing { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public bool HasPosition => Latitude is not null && Longitude is not null;

    private LocationShare() { } // EF

    private LocationShare(Guid userId) => UserId = userId;

    // Starts with sharing off: the pilgrim must opt in.
    public static Result<LocationShare> Create(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return LocationErrors.UserRequired;
        }

        return new LocationShare(userId);
    }

    public bool IsStale(DateTimeOffset nowUtc) => UpdatedAtUtc is not { } at || nowUtc - at > StaleAfter;

    public void StartSharing() => IsSharing = true;

    public void StopSharing()
    {
        if (!IsSharing && !HasPosition)
        {
            return;
        }

        IsSharing = false;
        Latitude = null;
        Longitude = null;
        AccuracyMeters = null;
        UpdatedAtUtc = null;
        Raise(new MemberStoppedSharingLocation(UserId));
    }

    public Result UpdatePosition(double latitude, double longitude, double? accuracyMeters, DateTimeOffset nowUtc)
    {
        if (!IsSharing) return Result.Failure(LocationErrors.SharingDisabled);
        if (latitude is < -90 or > 90 || double.IsNaN(latitude)) return Result.Failure(LocationErrors.InvalidLatitude);
        if (longitude is < -180 or > 180 || double.IsNaN(longitude)) return Result.Failure(LocationErrors.InvalidLongitude);
        if (accuracyMeters is < 0 || (accuracyMeters is { } a && double.IsNaN(a))) return Result.Failure(LocationErrors.InvalidAccuracy);

        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
        UpdatedAtUtc = nowUtc;
        Raise(new MemberLocationUpdated(UserId, latitude, longitude, accuracyMeters, nowUtc));
        return Result.Success();
    }
}
