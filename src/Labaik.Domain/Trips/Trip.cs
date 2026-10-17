using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips.Enums;
using Labaik.Domain.Trips.Events;

namespace Labaik.Domain.Trips;

// A company's product: a scheduled Umrah/Hajj trip that groups are formed under.
// Distinct from Journey (a pilgrim's personal onboarding); never merged with it.
public sealed class Trip : AggregateRoot
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PilgrimageType PilgrimageType { get; private set; }
    public DateOnly DepartureDate { get; private set; }
    public DateOnly? ReturnDate { get; private set; }
    public string DeparturePlace { get; private set; } = string.Empty;
    public int ExpectedPilgrims { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private List<string> _destinations = [];
    public IReadOnlyList<string> Destinations => _destinations.AsReadOnly();

    // Trip program, ordered by date (empty until set).
    private List<ItineraryDay> _itinerary = [];
    public IReadOnlyList<ItineraryDay> Itinerary => _itinerary.AsReadOnly();
    public DateTimeOffset? ItineraryUpdatedAtUtc { get; private set; }

    private Trip() { } // EF

    private Trip(Guid companyId, DateTimeOffset createdAtUtc)
    {
        CompanyId = companyId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<Trip> Create(Guid companyId, TripDetails details, DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty)
        {
            return TripErrors.CompanyRequired;
        }

        var trip = new Trip(companyId, createdAtUtc);
        var update = trip.Update(details);
        if (update.IsFailure)
        {
            return update.Error!;
        }

        trip.Raise(new TripCreated(trip.Id, companyId));
        return trip;
    }

    // Validates everything first, so a failure changes nothing.
    public Result Update(TripDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name)) return Result.Failure(TripErrors.NameRequired);
        if (string.IsNullOrWhiteSpace(details.DeparturePlace)) return Result.Failure(TripErrors.DeparturePlaceRequired);

        var name = details.Name.Trim();
        var place = details.DeparturePlace.Trim();
        var notes = string.IsNullOrWhiteSpace(details.Notes) ? null : details.Notes.Trim();
        if (name.Length > TripErrors.NameMaxLength || place.Length > TripErrors.PlaceMaxLength
            || notes is { Length: > TripErrors.NotesMaxLength })
        {
            return Result.Failure(TripErrors.TooLong);
        }

        var destinations = details.Destinations.Select(d => d?.Trim() ?? string.Empty).ToList();
        if (destinations.Count == 0) return Result.Failure(TripErrors.DestinationsRequired);
        if (destinations.Count > TripErrors.MaxDestinations
            || destinations.Any(d => d.Length == 0 || d.Length > TripErrors.DestinationMaxLength))
        {
            return Result.Failure(TripErrors.InvalidDestination);
        }

        if (details.ReturnDate is { } back && back < details.DepartureDate)
        {
            return Result.Failure(TripErrors.ReturnBeforeDeparture);
        }

        if (details.ExpectedPilgrims is < 1 or > TripErrors.MaxExpectedPilgrims)
        {
            return Result.Failure(TripErrors.InvalidExpectedPilgrims);
        }

        Name = name;
        PilgrimageType = details.PilgrimageType;
        _destinations = destinations;
        DepartureDate = details.DepartureDate;
        ReturnDate = details.ReturnDate;
        DeparturePlace = place;
        ExpectedPilgrims = details.ExpectedPilgrims;
        Notes = notes;
        return Result.Success();
    }

    // Replaces the whole program. Validates everything first, so a failure changes nothing.
    // Who may call this (company owner or a supervisor of the trip) is checked by the application layer,
    // which knows the trip's groups.
    public Result ReplaceItinerary(IReadOnlyList<ItineraryDayInput> days, DateTimeOffset nowUtc)
    {
        if (days.Count > TripErrors.MaxItineraryDays || days.Any(d => d.Activities.Count > TripErrors.MaxActivitiesPerDay))
        {
            return Result.Failure(TripErrors.ItineraryTooLarge);
        }

        if (days.Select(d => d.Date).Distinct().Count() != days.Count)
        {
            return Result.Failure(TripErrors.DuplicateItineraryDay);
        }

        var lastDay = ReturnDate ?? DateOnly.MaxValue;
        if (days.Any(d => d.Date < DepartureDate || d.Date > lastDay))
        {
            return Result.Failure(TripErrors.ItineraryDayOutsideTrip);
        }

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static bool TooLong(string? s) => s is { Length: > TripErrors.ActivityTextMaxLength };

        var built = new List<ItineraryDay>(days.Count);
        foreach (var day in days.OrderBy(d => d.Date))
        {
            var dayTitle = Clean(day.Title);
            var activities = new List<ItineraryActivity>(day.Activities.Count);
            foreach (var a in day.Activities)
            {
                var title = Clean(a.Title);
                var place = Clean(a.Place);
                if (title is null || TooLong(title) || TooLong(place))
                {
                    return Result.Failure(TripErrors.InvalidActivity);
                }

                activities.Add(new ItineraryActivity(title, a.TimeUtc, place));
            }

            if (TooLong(dayTitle))
            {
                return Result.Failure(TripErrors.InvalidActivity);
            }

            // Timed activities in time order; untimed ones keep their given order after them.
            var ordered = activities.Where(x => x.TimeUtc is not null).OrderBy(x => x.TimeUtc)
                .Concat(activities.Where(x => x.TimeUtc is null)).ToList();
            built.Add(new ItineraryDay(day.Date, dayTitle, ordered));
        }

        _itinerary = built;
        ItineraryUpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public TripStatus StatusOn(DateOnly todayUtc) =>
        todayUtc >= DepartureDate ? TripStatus.Departed : TripStatus.Upcoming;
}

// Editable fields of a trip (create and update take the same shape).
public sealed record TripDetails(
    string Name,
    PilgrimageType PilgrimageType,
    IReadOnlyList<string> Destinations,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    string DeparturePlace,
    int ExpectedPilgrims,
    string? Notes);
