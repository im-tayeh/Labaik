using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Commands.SetCompanyStopped;

// Admin suspends (status 2 Stopped) or resumes a company. Idempotent. While stopped, the owner (and
// supervisors on no other active company) cannot sign in or refresh (403 Company.Stopped), and tokens
// already issued stop working on the next request (AccountBlockMiddleware).
public sealed record SetCompanyStoppedCommand(Guid CompanyId, bool IsStopped) : IRequest<Result<AdminCompanyDto>>;

public sealed class SetCompanyStoppedCommandHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IAccountAccessCache accessCache) : IRequestHandler<SetCompanyStoppedCommand, Result<AdminCompanyDto>>
{
    public async Task<Result<AdminCompanyDto>> Handle(SetCompanyStoppedCommand request, CancellationToken cancellationToken)
    {
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (company is null)
        {
            return CompanyErrors.NotFoundById;
        }

        if (request.IsStopped) company.Stop();
        else company.Resume();

        await context.SaveChangesAsync(cancellationToken);
        accessCache.Reset();
        return await company.ToAdminDtoAsync(context, userDirectory, timeProvider.TodayUtc(), cancellationToken);
    }
}
