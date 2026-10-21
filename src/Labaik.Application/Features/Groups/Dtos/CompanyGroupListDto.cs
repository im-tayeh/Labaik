namespace Labaik.Application.Features.Groups.Dtos;

public sealed record CompanyGroupListItemDto(
    Guid Id, string Name, string JoinCode, Guid TripId, string TripName,
    DateOnly TripDepartureDate, int PilgrimCount, string? SupervisorName,
    Guid? SupervisorUserId = null, string? Notes = null, int? Capacity = null);
