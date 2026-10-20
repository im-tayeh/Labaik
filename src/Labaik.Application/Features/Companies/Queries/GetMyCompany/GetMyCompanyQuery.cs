using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Companies.Queries.GetMyCompany;

public sealed record GetMyCompanyQuery : IRequest<Result<CompanyDto>>;

public sealed class SupportSettings
{
    public const string SectionName = "Support";
    public string Email { get; init; } = string.Empty;
}

public sealed class GetMyCompanyQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOptions<SupportSettings> supportOptions) : IRequestHandler<GetMyCompanyQuery, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(GetMyCompanyQuery request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var company = await context.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        return company.ToDto(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), supportOptions.Value.Email);
    }
}
