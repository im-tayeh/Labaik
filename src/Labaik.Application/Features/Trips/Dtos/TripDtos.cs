using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips;
using Labaik.Domain.Trips.Enums;

namespace Labaik.Application.Features.Trips.Dtos;

// List row (company trips screen).
public sealed record TripSummaryDto(
    Guid Id,
    string Name,
    PilgrimageType PilgrimageType,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    int ExpectedPilgrims,
    TripStatus Status);

// Trip details screen.
public sealed record TripDto(
    Guid Id,
    string Name,
    PilgrimageType PilgrimageType,
    IReadOnlyList<string> Destinations,
    DateOnly DepartureDate,
    DateOnly? ReturnDate,
    string DeparturePlace,
    int ExpectedPilgrims,
    string? Notes,
    TripStatus Status,
    DateTimeOffset CreatedAtUtc);

public static class TripMapper
{
    public static TripDto ToDto(this Trip t, DateOnly todayUtc) => new(
        t.Id, t.Name, t.PilgrimageType, t.Destinations, t.DepartureDate, t.ReturnDate,
        t.DeparturePlace, t.ExpectedPilgrims, t.Notes, t.StatusOn(todayUtc), t.CreatedAtUtc);

    public static TripSummaryDto ToSummary(this Trip t, DateOnly todayUtc) => new(
        t.Id, t.Name, t.PilgrimageType, t.DepartureDate, t.ReturnDate, t.ExpectedPilgrims, t.StatusOn(todayUtc));

    public static DateOnly TodayUtc(this TimeProvider timeProvider) => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
