using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups;

namespace Labaik.Application.Features.Groups.Dtos;

// A company-managed group as the company sees it. Supervisor fields are null until one is assigned.
public sealed record CompanyGroupDto(
    Guid Id,
    Guid TripId,
    string Name,
    string JoinCode,
    Guid? SupervisorId,
    string? SupervisorName,
    int PilgrimCount);

public static class CompanyGroupMapper
{
    public static async Task<CompanyGroupDto> ToCompanyDtoAsync(
        this Group g, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var supervisorName = g.LeaderId is { } id ? await userDirectory.GetFullNameAsync(id, cancellationToken) : null;
        return new CompanyGroupDto(g.Id, g.TripId!.Value, g.Name, g.JoinCode, g.LeaderId, supervisorName, g.PilgrimCount);
    }
}
