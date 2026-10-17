using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Trips;

public static class TripErrors
{
    public const int NameMaxLength = 150;
    public const int PlaceMaxLength = 200;
    public const int DestinationMaxLength = 100;
    public const int MaxDestinations = 10;
    public const int NotesMaxLength = 2000;
    public const int MaxExpectedPilgrims = 10_000;

    public static Error CompanyRequired =>
        Error.Validation("Trip.CompanyRequired", "A trip must belong to a company.");

    public static Error NameRequired =>
        Error.Validation("Trip.NameRequired", "Trip name is required.");

    public static Error DeparturePlaceRequired =>
        Error.Validation("Trip.DeparturePlaceRequired", "Departure place is required.");

    public static Error DestinationsRequired =>
        Error.Validation("Trip.DestinationsRequired", "At least one destination is required.");

    public static Error InvalidDestination =>
        Error.Validation("Trip.InvalidDestination",
            $"Destinations must be non-empty, at most {DestinationMaxLength} characters, and at most {MaxDestinations}.");

    public static Error ReturnBeforeDeparture =>
        Error.Validation("Trip.ReturnBeforeDeparture", "Return date cannot be before the departure date.");

    public static Error InvalidExpectedPilgrims =>
        Error.Validation("Trip.InvalidExpectedPilgrims", $"Expected pilgrims must be between 1 and {MaxExpectedPilgrims}.");

    public static Error TooLong =>
        Error.Validation("Trip.TooLong", "A text field exceeds its maximum length.");

    public static Error NotFound =>
        Error.NotFound("Trip.NotFound", "The trip was not found.");
}
