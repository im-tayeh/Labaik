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

    public const int MaxItineraryDays = 60;
    public const int MaxActivitiesPerDay = 30;
    public const int ActivityTextMaxLength = 200;

    public static Error ItineraryTooLarge =>
        Error.Validation("Trip.ItineraryTooLarge",
            $"An itinerary has at most {MaxItineraryDays} days and {MaxActivitiesPerDay} activities per day.");

    public static Error ItineraryDayOutsideTrip =>
        Error.Validation("Trip.ItineraryDayOutsideTrip", "Every itinerary day must fall between the departure and return dates.");

    public static Error DuplicateItineraryDay =>
        Error.Validation("Trip.DuplicateItineraryDay", "Each date may appear only once in the itinerary.");

    public static Error InvalidActivity =>
        Error.Validation("Trip.InvalidActivity",
            $"Each activity needs a title; title, place and day title are at most {ActivityTextMaxLength} characters.");

    public static Error CannotEditItinerary =>
        Error.Forbidden("Trip.CannotEditItinerary", "Only the company or a supervisor of this trip can edit its program.");

    public static Error NotFound =>
        Error.NotFound("Trip.NotFound", "The trip was not found.");

    public static Error HasPilgrims =>
        Error.Conflict("Trip.HasPilgrims", "Remove the pilgrims from this trip's groups before deleting it.");
}
