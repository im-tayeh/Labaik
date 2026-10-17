using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetTripGroups;

// The groups under one of my company's trips.
public sealed record GetTripGroupsQuery(Guid TripId) : IRequest<Result<IReadOnlyList<CompanyGroupDto>>>;

public sealed class GetTripGroupsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetTripGroupsQuery, Result<IReadOnlyList<CompanyGroupDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyGroupDto>>> Handle(GetTripGroupsQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        if (!await context.Trips.AnyAsync(t => t.Id == request.TripId && t.CompanyId == companyId.Value, cancellationToken))
        {
            return TripErrors.NotFound;
        }

        var groups = await context.Groups
            .AsNoTracking()
            .Where(g => g.TripId == request.TripId && g.CompanyId == companyId.Value)
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                g.Id, g.Name, g.JoinCode, g.LeaderId,
                Pilgrims = g.Members.Count(m => m.Role == GroupMemberRole.Member)
            })
            .ToListAsync(cancellationToken);

        var names = await userDirectory.GetFullNamesAsync(
            groups.Where(g => g.LeaderId != null).Select(g => g.LeaderId!.Value).Distinct().ToList(), cancellationToken);

        return groups
            .Select(g => new CompanyGroupDto(
                g.Id, request.TripId, g.Name, g.JoinCode, g.LeaderId,
                g.LeaderId is { } id ? names.GetValueOrDefault(id) : null, g.Pilgrims))
            .ToList();
    }
}
