using Labaik.Application.Features.Journeys.Dtos;
using Labaik.Domain.Journeys;

namespace Labaik.Application.Features.Journeys.Mappers;

public static class JourneyMapper
{
    public static JourneyDto ToDto(this Journey j) => new(
        j.Id, j.PilgrimageType, j.IsFirstTime, j.TravelParty, j.Status,
        j.ArrivalPlan is null ? null : new ArrivalPlanDto(j.ArrivalPlan.Mode, j.ArrivalPlan.EntryPoint),
        j.Stays.Select(s => new StayDto(s.CityName, s.AccommodationType, s.PlaceName, s.ArrivalAtUtc)).ToList());
}