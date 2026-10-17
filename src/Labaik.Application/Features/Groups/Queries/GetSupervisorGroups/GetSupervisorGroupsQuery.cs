using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetSupervisorGroups;

// One row of the supervisor's group picker. Trip fields are null for standalone groups.
public sealed record SupervisorGroupDto(
    Guid Id,
    string Name,
    Guid? TripId,
    string? TripName,
    DateOnly? DepartureDate,
    int PilgrimCount,
    int PresentCount,
    int AbsentCount);

// Groups the current user supervises, soonest departure first (standalone groups last).
public sealed record GetSupervisorGroupsQuery : IRequest<Result<IReadOnlyList<SupervisorGroupDto>>>;

public sealed class GetSupervisorGroupsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetSupervisorGroupsQuery, Result<IReadOnlyList<SupervisorGroupDto>>>
{
    public async Task<Result<IReadOnlyList<SupervisorGroupDto>>> Handle(GetSupervisorGroupsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var rows = await (
                from g in context.Groups.AsNoTracking()
                where g.LeaderId == userId
                join t in context.Trips.AsNoTracking() on g.TripId equals t.Id into trips
                from t in trips.DefaultIfEmpty()
                select new
                {
                    g.Id,
                    g.Name,
                    g.TripId,
                    TripName = t == null ? null : t.Name,
                    DepartureDate = t == null ? (DateOnly?)null : t.DepartureDate,
                    Pilgrims = g.Members.Count(m => m.Role == GroupMemberRole.Member),
                    Present = g.Members.Count(m => m.Role == GroupMemberRole.Member && m.AttendanceStatus == AttendanceStatus.Present)
                })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.DepartureDate is null)
            .ThenBy(r => r.DepartureDate)
            .ThenBy(r => r.Name)
            .Select(r => new SupervisorGroupDto(
                r.Id, r.Name, r.TripId, r.TripName, r.DepartureDate, r.Pilgrims, r.Present, r.Pilgrims - r.Present))
            .ToList();
    }
}
