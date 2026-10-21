using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetCompanyGroups;

public sealed record GetCompanyGroupsQuery : IRequest<Result<IReadOnlyList<CompanyGroupListItemDto>>>;

public sealed class GetCompanyGroupsQueryHandler(
    IAppDbContext context, ICurrentUser currentUser, IUserDirectory userDirectory)
    : IRequestHandler<GetCompanyGroupsQuery, Result<IReadOnlyList<CompanyGroupListItemDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyGroupListItemDto>>> Handle(
        GetCompanyGroupsQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return companyId.Error!;

        var rows = await (from g in context.Groups.AsNoTracking()
                          join trip in context.Trips.AsNoTracking() on g.TripId equals (Guid?)trip.Id
                          where g.CompanyId == companyId.Value
                          orderby trip.DepartureDate, g.Name
                          select new
                          {
                              g.Id, g.Name, g.JoinCode, TripId = trip.Id, TripName = trip.Name,
                              TripDepartureDate = trip.DepartureDate, g.LeaderId,
                              g.Notes, g.Capacity,
                              PilgrimCount = g.Members.Count(member => member.Role == GroupMemberRole.Member)
                          }).ToListAsync(cancellationToken);

        var names = await userDirectory.GetFullNamesAsync(
            rows.Where(row => row.LeaderId.HasValue).Select(row => row.LeaderId!.Value).Distinct().ToList(), cancellationToken);
        return rows.Select(row => new CompanyGroupListItemDto(
            row.Id, row.Name, row.JoinCode, row.TripId, row.TripName, row.TripDepartureDate, row.PilgrimCount,
            row.LeaderId is { } supervisorId ? names.GetValueOrDefault(supervisorId) : null,
            row.LeaderId, row.Notes, row.Capacity)).ToList();
    }
}
