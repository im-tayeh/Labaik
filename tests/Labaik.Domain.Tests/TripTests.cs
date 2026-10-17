using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips;
using Labaik.Domain.Trips.Enums;
using Labaik.Domain.Trips.Events;

namespace Labaik.Domain.Tests;

public class TripTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Departure = new(2026, 12, 10);

    private static TripDetails Details(
        string name = "December Umrah", string[]? destinations = null, DateOnly? returnDate = null, int expected = 40) =>
        new(name, PilgrimageType.Umrah, destinations ?? [" Makkah ", "Madinah"], Departure,
            returnDate ?? Departure.AddDays(10), "Amman", expected, null);

    [Fact]
    public void Create_trims_values_and_raises_event()
    {
        var trip = Trip.Create(Guid.NewGuid(), Details(), Now).Value;

        Assert.Equal(["Makkah", "Madinah"], trip.Destinations);
        Assert.IsType<TripCreated>(Assert.Single(trip.DomainEvents));
    }

    [Fact]
    public void Create_validates_fields()
    {
        var company = Guid.NewGuid();

        Assert.Equal("Trip.CompanyRequired", Trip.Create(Guid.Empty, Details(), Now).Error!.Code);
        Assert.Equal("Trip.NameRequired", Trip.Create(company, Details(name: " "), Now).Error!.Code);
        Assert.Equal("Trip.DestinationsRequired", Trip.Create(company, Details(destinations: []), Now).Error!.Code);
        Assert.Equal("Trip.InvalidDestination", Trip.Create(company, Details(destinations: ["Makkah", " "]), Now).Error!.Code);
        Assert.Equal("Trip.ReturnBeforeDeparture", Trip.Create(company, Details(returnDate: Departure.AddDays(-1)), Now).Error!.Code);
        Assert.Equal("Trip.InvalidExpectedPilgrims", Trip.Create(company, Details(expected: 0), Now).Error!.Code);
    }

    [Fact]
    public void Update_is_atomic()
    {
        var trip = Trip.Create(Guid.NewGuid(), Details(), Now).Value;

        var result = trip.Update(Details(name: "Renamed", expected: 0));

        Assert.True(result.IsFailure);
        Assert.Equal("December Umrah", trip.Name);
    }

    [Fact]
    public void Status_is_computed_from_the_departure_date()
    {
        var trip = Trip.Create(Guid.NewGuid(), Details(), Now).Value;

        Assert.Equal(TripStatus.Upcoming, trip.StatusOn(Departure.AddDays(-1)));
        Assert.Equal(TripStatus.Departed, trip.StatusOn(Departure));
    }
}
