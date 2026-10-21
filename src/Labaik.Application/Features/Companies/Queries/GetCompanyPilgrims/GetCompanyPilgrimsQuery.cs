using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Queries.GetCompanyPilgrims;

public sealed record CompanyPilgrimDto(Guid UserId, string FullName, string Email, string? PhoneNumber,
    IReadOnlyList<Guid> GroupIds);

public sealed record GetCompanyPilgrimsQuery(string? Search = null, int Take = 100)
    : IRequest<Result<IReadOnlyList<CompanyPilgrimDto>>>;

public sealed class GetCompanyPilgrimsQueryValidator : AbstractValidator<GetCompanyPilgrimsQuery>
{
    public GetCompanyPilgrimsQueryValidator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class GetCompanyPilgrimsQueryHandler(
    IAppDbContext context, ICurrentUser currentUser, IUserDirectory userDirectory)
    : IRequestHandler<GetCompanyPilgrimsQuery, Result<IReadOnlyList<CompanyPilgrimDto>>>
{
    public async Task<Result<IReadOnlyList<CompanyPilgrimDto>>> Handle(
        GetCompanyPilgrimsQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return companyId.Error!;

        var memberships = await context.Groups.AsNoTracking()
            .Where(g => g.CompanyId == companyId.Value)
            .SelectMany(g => g.Members.Where(m => m.Role == GroupMemberRole.Member)
                .Select(m => new { m.UserId, GroupId = g.Id }))
            .ToListAsync(cancellationToken);
        var groupIds = memberships.GroupBy(m => m.UserId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Guid>)x.Select(m => m.GroupId).Distinct().ToList());
        var contacts = await userDirectory.GetContactsAsync(groupIds.Keys.ToList(), cancellationToken);
        IEnumerable<CompanyPilgrimDto> rows = contacts.Values
            .Where(c => groupIds.ContainsKey(c.Id) && c.Email is not null)
            .Select(c => new CompanyPilgrimDto(c.Id, c.FullName, c.Email!, c.PhoneNumber, groupIds[c.Id]));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            rows = rows.Where(x => x.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        return rows.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase).Take(request.Take).ToList();
    }
}
