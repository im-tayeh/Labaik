using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Queries.GetCompanySupervisors;

// My company's supervisor roster, by name. GroupsCount = my groups they currently supervise.
public sealed record GetCompanySupervisorsQuery : IRequest<Result<IReadOnlyList<CompanySupervisorDto>>>;

public sealed class GetCompanySupervisorsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetCompanySupervisorsQuery, Result<IReadOnlyList<CompanySupervisorDto>>>
{
    public async Task<Result<IReadOnlyList<CompanySupervisorDto>>> Handle(
        GetCompanySupervisorsQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var id = companyId.Value;
        var roster = await context.Companies
            .AsNoTracking()
            .Where(c => c.Id == id)
            .SelectMany(c => c.Supervisors)
            .Select(s => new { s.UserId, s.AddedAtUtc })
            .ToListAsync(cancellationToken);

        var groupsBySupervisor = await context.Groups
            .Where(g => g.CompanyId == id && g.LeaderId != null)
            .GroupBy(g => g.LeaderId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var contacts = await userDirectory.GetContactsAsync(roster.Select(s => s.UserId).ToList(), cancellationToken);

        return roster
            .Select(s => contacts.TryGetValue(s.UserId, out var c)
                ? new CompanySupervisorDto(s.UserId, c.FullName, c.Email ?? string.Empty, c.PhoneNumber,
                    s.AddedAtUtc, groupsBySupervisor.GetValueOrDefault(s.UserId))
                : null)
            .OfType<CompanySupervisorDto>()
            .OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
