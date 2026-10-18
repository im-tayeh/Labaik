using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Queries.GetCompanyById;

public sealed record GetCompanyByIdQuery(Guid CompanyId) : IRequest<Result<AdminCompanyDto>>;

public sealed class GetCompanyByIdQueryHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<GetCompanyByIdQuery, Result<AdminCompanyDto>>
{
    public async Task<Result<AdminCompanyDto>> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
    {
        var company = await context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (company is null)
        {
            return CompanyErrors.NotFoundById;
        }

        return await company.ToAdminDtoAsync(context, userDirectory, timeProvider.TodayUtc(), cancellationToken);
    }
}
