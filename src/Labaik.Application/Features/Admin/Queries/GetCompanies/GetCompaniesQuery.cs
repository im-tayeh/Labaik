using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Queries.GetCompanies;

// Admin dashboard companies table, newest first.
public sealed record GetCompaniesQuery : IRequest<Result<IReadOnlyList<AdminCompanyListItemDto>>>;

public sealed class GetCompaniesQueryHandler(
    IAppDbContext context,
    TimeProvider timeProvider) : IRequestHandler<GetCompaniesQuery, Result<IReadOnlyList<AdminCompanyListItemDto>>>
{
    public async Task<Result<IReadOnlyList<AdminCompanyListItemDto>>> Handle(
        GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var today = timeProvider.TodayUtc();

        var rows = await context.Companies
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new { c.Id, c.Name, c.LogoUrl, c.ContractStartDate, c.ContractEndDate, c.IsStopped })
            .ToListAsync(cancellationToken);

        var pilgrims = await context.CountActivePilgrimsByCompanyAsync(today, cancellationToken);

        return rows
            .Select(c => new AdminCompanyListItemDto(
                c.Id, c.Name, c.LogoUrl, pilgrims.GetValueOrDefault(c.Id),
                c.ContractStartDate, c.ContractEndDate,
                Company.StatusOf(c.IsStopped, c.ContractEndDate, today),
                Company.DaysRemainingOf(c.ContractEndDate, today)))
            .ToList();
    }
}
