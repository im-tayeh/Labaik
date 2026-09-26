using Labaik.Domain.Journeys.Enums;

namespace Labaik.Application.Features.Journeys.Dtos;

public sealed record StayDto(string CityName, AccommodationType AccommodationType, string PlaceName, DateTimeOffset ArrivalAtUtc);

public sealed record ArrivalPlanDto(TransportMode Mode, string EntryPoint);

public sealed record JourneyDto(
    Guid Id,
    PilgrimageType PilgrimageType,
    bool IsFirstTime,
    TravelParty TravelParty,
    JourneyStatus Status,
    ArrivalPlanDto? ArrivalPlan,
    IReadOnlyList<StayDto> Stays);