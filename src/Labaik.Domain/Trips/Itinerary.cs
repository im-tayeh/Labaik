namespace Labaik.Domain.Trips;

// The trip program, stored as one JSON document on the trip and always replaced as a whole.
// Plain classes (not entities): they have no identity outside the trip.
public sealed class ItineraryDay
{
    public DateOnly Date { get; private set; }
    public string? Title { get; private set; }

    private List<ItineraryActivity> _activities = [];
    public IReadOnlyList<ItineraryActivity> Activities => _activities.AsReadOnly();

    private ItineraryDay() { } // EF

    internal ItineraryDay(DateOnly date, string? title, List<ItineraryActivity> activities)
    {
        Date = date;
        Title = title;
        _activities = activities;
    }
}

public sealed class ItineraryActivity
{
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset? TimeUtc { get; private set; }
    public string? Place { get; private set; }

    private ItineraryActivity() { } // EF

    internal ItineraryActivity(string title, DateTimeOffset? timeUtc, string? place)
    {
        Title = title;
        TimeUtc = timeUtc;
        Place = place;
    }
}

// Input shapes for Trip.ReplaceItinerary.
public sealed record ItineraryDayInput(DateOnly Date, string? Title, IReadOnlyList<ItineraryActivityInput> Activities);

public sealed record ItineraryActivityInput(string Title, DateTimeOffset? TimeUtc, string? Place);
