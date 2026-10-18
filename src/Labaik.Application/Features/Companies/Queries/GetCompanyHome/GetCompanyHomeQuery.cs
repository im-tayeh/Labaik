using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Queries.GetCompanyHome;

// Company dashboard numbers:
// - ActiveTrips: trips not over yet (no return date, or returning today or later, UTC).
// - PilgrimsCount: distinct pilgrims across all the company's groups (someone in two groups counts once).
// - SupervisorsCount: supervisors on the company's roster (assigned to a group or not).
// - GroupsCount: all the company's groups.
public sealed record CompanyHomeDto(int ActiveTrips, int PilgrimsCount, int SupervisorsCount, int GroupsCount);

public sealed record GetCompanyHomeQuery : IRequest<Result<CompanyHomeDto>>;

public sealed class GetCompanyHomeQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<GetCompanyHomeQuery, Result<CompanyHomeDto>>
{
    public async Task<Result<CompanyHomeDto>> Handle(GetCompanyHomeQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var id = companyId.Value;
        var today = timeProvider.TodayUtc();
        var groups = context.Groups.Where(g => g.CompanyId == id);

        var activeTrips = await context.Trips
            .CountAsync(t => t.CompanyId == id && (t.ReturnDate == null || t.ReturnDate >= today), cancellationToken);

        var groupsCount = await groups.CountAsync(cancellationToken);

        var supervisorsCount = await context.CountRosterSupervisorsAsync(id, cancellationToken);

        var pilgrimsCount = await groups
            .SelectMany(g => g.Members)
            .Where(m => m.Role == GroupMemberRole.Member)
            .Select(m => m.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new CompanyHomeDto(activeTrips, pilgrimsCount, supervisorsCount, groupsCount);
    }
}
