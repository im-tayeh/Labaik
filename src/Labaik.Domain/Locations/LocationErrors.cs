using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Locations;

public static class LocationErrors
{
    public static Error UserRequired =>
        Error.Validation("Location.UserRequired", "Location sharing must belong to a user.");

    public static Error InvalidLatitude =>
        Error.Validation("Location.InvalidLatitude", "Latitude must be between -90 and 90.");

    public static Error InvalidLongitude =>
        Error.Validation("Location.InvalidLongitude", "Longitude must be between -180 and 180.");

    public static Error InvalidAccuracy =>
        Error.Validation("Location.InvalidAccuracy", "Accuracy must be zero or positive.");

    public static Error SharingDisabled =>
        Error.Conflict("Location.SharingDisabled", "Turn on location sharing before sending a location.");
}
