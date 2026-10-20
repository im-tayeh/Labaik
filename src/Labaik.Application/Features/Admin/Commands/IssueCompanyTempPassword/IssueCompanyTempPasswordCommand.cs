using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Application.Common.Provisioning;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Admin.Commands.IssueCompanyTempPassword;

public sealed record IssueCompanyTempPasswordCommand(Guid CompanyId) : IRequest<Result<TemporaryPasswordDto>>;

public sealed class IssueCompanyTempPasswordCommandHandler(
    IAppDbContext context, IIdentityService identityService, IEmailSender emailSender,
    IOptions<AppLinksSettings> appLinks, ILogger<IssueCompanyTempPasswordCommandHandler> logger)
    : IRequestHandler<IssueCompanyTempPasswordCommand, Result<TemporaryPasswordDto>>
{
    public async Task<Result<TemporaryPasswordDto>> Handle(
        IssueCompanyTempPasswordCommand request, CancellationToken cancellationToken)
    {
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (company is null) return Error.NotFound("Company.NotFound", "No company found.");

        var user = await identityService.GetAuthUserByIdAsync(company.OwnerUserId, cancellationToken);
        if (user.IsFailure) return user.Error!;
        var password = await identityService.IssueTemporaryPasswordAsync(company.OwnerUserId, cancellationToken);
        if (password.IsFailure) return password.Error!;

        var account = new ProvisionedAccount(user.Value.Id, user.Value.Email, user.Value.FullName, password.Value);
        var sent = await emailSender.TrySendAsync(account.Email,
            AccountEmails.For(account, "company", company.Name, appLinks.Value.DownloadUrl), logger, cancellationToken);
        return new TemporaryPasswordDto(password.Value, sent);
    }
}
