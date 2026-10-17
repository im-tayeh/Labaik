using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Queries.GetMyCompany;

public sealed record GetMyCompanyQuery : IRequest<Result<CompanyDto>>;

public sealed class GetMyCompanyQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyCompanyQuery, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(GetMyCompanyQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        return await context.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId.Value)
            .Select(c => new CompanyDto(c.Id, c.Name))
            .FirstAsync(cancellationToken);
    }
}
