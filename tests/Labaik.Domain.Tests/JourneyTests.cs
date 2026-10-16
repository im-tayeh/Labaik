using Labaik.Domain.Journeys;
using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Tests;

public class JourneyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static Journey NewJourney() =>
        Journey.Create(Guid.NewGuid(), PilgrimageType.Umrah, isFirstTime: true, TravelParty.Solo).Value;

    private static Journey ReadyJourney()
    {
        var journey = NewJourney();
        journey.SetArrivalPlan(TransportMode.Flight, "Jeddah");
        journey.AddStay("Makkah", AccommodationType.Hotel, "Hotel A", Now);
        Assert.True(journey.MarkReady().IsSuccess);
        return journey;
    }

    [Fact]
    public void Create_starts_as_draft_and_raises_event()
    {
        var journey = NewJourney();

        Assert.Equal(JourneyStatus.Draft, journey.Status);
        Assert.Single(journey.DomainEvents);
    }

    [Fact]
    public void Create_requires_user()
    {
        var result = Journey.Create(Guid.Empty, PilgrimageType.Umrah, true, TravelParty.Solo);

        Assert.True(result.IsFailure);
        Assert.Equal("Journey.UserRequired", result.Error!.Code);
    }

    [Fact]
    public void MarkReady_requires_arrival_plan_and_a_stay()
    {
        var journey = NewJourney();
        Assert.Equal("Journey.ArrivalPlanRequired", journey.MarkReady().Error!.Code);

        journey.SetArrivalPlan(TransportMode.Flight, "Jeddah");
        Assert.Equal("Journey.StayRequired", journey.MarkReady().Error!.Code);

        journey.AddStay("Makkah", AccommodationType.Hotel, "Hotel A", Now);
        Assert.True(journey.MarkReady().IsSuccess);
        Assert.Equal(JourneyStatus.Ready, journey.Status);
    }

    [Fact]
    public void Ready_journey_is_still_editable()
    {
        var journey = ReadyJourney();

        Assert.True(journey.UpdateDetails(false, TravelParty.Family).IsSuccess);
        Assert.True(journey.SetArrivalPlan(TransportMode.Bus, "Madinah").IsSuccess);
        Assert.Equal(TravelParty.Family, journey.TravelParty);
        Assert.Equal("Madinah", journey.ArrivalPlan!.EntryPoint);
    }

    [Fact]
    public void Active_journey_is_not_editable()
    {
        var journey = ReadyJourney();
        Assert.True(journey.Activate().IsSuccess);

        var result = journey.UpdateDetails(false, TravelParty.Group);

        Assert.Equal("Journey.NotEditable", result.Error!.Code);
    }

    [Fact]
    public void ReplaceStays_replaces_all_stays()
    {
        var journey = ReadyJourney();

        var result = journey.ReplaceStays(
        [
            new StayDetails(" Madinah ", AccommodationType.Other, "Flat", Now.AddDays(3)),
            new StayDetails("Makkah", AccommodationType.Hotel, "Hotel B", Now.AddDays(1))
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, journey.Stays.Count);
        Assert.Contains(journey.Stays, s => s.CityName == "Madinah"); // trimmed
    }

    [Fact]
    public void ReplaceStays_is_atomic_when_one_stay_is_invalid()
    {
        var journey = ReadyJourney();

        var result = journey.ReplaceStays(
        [
            new StayDetails("Madinah", AccommodationType.Other, "Flat", Now),
            new StayDetails("Makkah", AccommodationType.Hotel, " ", Now)
        ]);

        Assert.Equal("Journey.PlaceNameRequired", result.Error!.Code);
        Assert.Equal("Hotel A", Assert.Single(journey.Stays).PlaceName); // unchanged
    }

    [Fact]
    public void ReplaceStays_cannot_empty_a_ready_journey()
    {
        var journey = ReadyJourney();

        Assert.Equal("Journey.StayRequired", journey.ReplaceStays([]).Error!.Code);
    }

    [Fact]
    public void Status_transitions_are_guarded()
    {
        var journey = NewJourney();

        Assert.Equal("Journey.InvalidTransition", journey.Activate().Error!.Code);
        Assert.Equal("Journey.InvalidTransition", journey.Complete().Error!.Code);
    }
}
