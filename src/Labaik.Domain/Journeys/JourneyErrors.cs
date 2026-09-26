using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Journeys;

public static class JourneyErrors
{
    public static Error UserRequired =>
        Error.Validation("Journey.UserRequired", "A journey must belong to a user.");

    public static Error EntryPointRequired =>
        Error.Validation("Journey.EntryPointRequired", "Entry point is required.");

    public static Error PlaceNameRequired =>
        Error.Validation("Journey.PlaceNameRequired", "Accommodation place name is required.");

    public static Error NotEditable(JourneyStatus status) =>
        Error.Conflict("Journey.NotEditable", $"A journey in '{status}' state cannot be edited.");

    public static Error ArrivalPlanRequired =>
        Error.Validation("Journey.ArrivalPlanRequired", "Set an arrival plan before finishing setup.");

    public static Error StayRequired =>
        Error.Validation("Journey.StayRequired", "Add at least one stay before finishing setup.");

    public static Error InvalidStatusTransition(JourneyStatus from, JourneyStatus to) =>
        Error.Conflict("Journey.InvalidTransition", $"Cannot move a journey from '{from}' to '{to}'.");

    public static Error CityRequired => 
        Error.Validation("Journey.CityRequired", "City name is required.");
}