using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Companies.Commands.IssueSupervisorTempPassword;

public sealed record IssueSupervisorTempPasswordCommand(Guid UserId) : IRequest<Result<TemporaryPasswordDto>>;

public sealed class IssueSupervisorTempPasswordCommandHandler(
    IAppDbContext context, ICurrentUser currentUser, IIdentityService identityService, IEmailSender emailSender,
    IOptions<AppLinksSettings> appLinks, ILogger<IssueSupervisorTempPasswordCommandHandler> logger)
    : IRequestHandler<IssueSupervisorTempPasswordCommand, Result<TemporaryPasswordDto>>
{
    public async Task<Result<TemporaryPasswordDto>> Handle(
        IssueSupervisorTempPasswordCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return companyId.Error!;
        var company = await context.Companies.FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        if (!company.HasSupervisor(request.UserId)) return Error.NotFound("Company.SupervisorNotFound", "No supervisor found on this company's roster.");

        var user = await identityService.GetAuthUserByIdAsync(request.UserId, cancellationToken);
        if (user.IsFailure) return user.Error!;
        var password = await identityService.IssueTemporaryPasswordAsync(request.UserId, cancellationToken);
        if (password.IsFailure) return password.Error!;

        var account = new ProvisionedAccount(user.Value.Id, user.Value.Email, user.Value.FullName, password.Value);
        var sent = await emailSender.TrySendAsync(account.Email,
            AccountEmails.For(account, "supervisor", company.Name, appLinks.Value.DownloadUrl), logger, cancellationToken);
        return new TemporaryPasswordDto(password.Value, sent);
    }
}
