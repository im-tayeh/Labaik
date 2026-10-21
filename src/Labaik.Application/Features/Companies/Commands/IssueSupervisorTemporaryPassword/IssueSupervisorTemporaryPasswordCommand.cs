using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Application.Features.Companies.Commands.IssueSupervisorTemporaryPassword;

// A company issues a new temporary password for one of its supervisors (e.g. they forgot it).
// The old password and sessions stop working; the new one is emailed and returned once so the
// company can also hand it over directly.
public sealed record IssueSupervisorTemporaryPasswordCommand(Guid UserId) : IRequest<Result<TemporaryPasswordDto>>;

public sealed record TemporaryPasswordDto(string TemporaryPassword);

public sealed class IssueSupervisorTemporaryPasswordCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IEmailSender emailSender,
    ILogger<IssueSupervisorTemporaryPasswordCommandHandler> logger)
    : IRequestHandler<IssueSupervisorTemporaryPasswordCommand, Result<TemporaryPasswordDto>>
{
    public async Task<Result<TemporaryPasswordDto>> Handle(IssueSupervisorTemporaryPasswordCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var company = await context.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        if (!company.HasSupervisor(request.UserId))
        {
            return CompanyErrors.SupervisorNotOnRoster;
        }

        var account = await identityService.IssueTemporaryPasswordAccountAsync(request.UserId, cancellationToken);
        if (account.IsFailure)
        {
            return account.Error!;
        }

        await emailSender.TrySendAsync(account.Value.Email,
            AccountEmails.TemporaryPassword(account.Value, company.Name), logger, cancellationToken);

        return new TemporaryPasswordDto(account.Value.TemporaryPassword!);
    }
}
