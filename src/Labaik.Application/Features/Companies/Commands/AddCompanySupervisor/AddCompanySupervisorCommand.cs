using FluentValidation;
using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Companies.Commands.AddCompanySupervisor;

// Company creates a supervisor account and puts it on its roster (Supervisor role, effective from
// the account's next token). New email: confirmed account + emailed temporary password.
// Registered email: linked as is, notice without a password.
public sealed record AddCompanySupervisorCommand(string FullName, string Email, string? PhoneNumber)
    : IRequest<Result<CompanySupervisorDto>>;

public sealed class AddCompanySupervisorCommandValidator : AbstractValidator<AddCompanySupervisorCommand>
{
    public AddCompanySupervisorCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(CompanyErrors.ContactNameMaxLength);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(CompanyErrors.EmailMaxLength);
        RuleFor(x => x.PhoneNumber).MaximumLength(CompanyErrors.PhoneMaxLength);
    }
}

public sealed class AddCompanySupervisorCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    IOptions<AppLinksSettings> appLinks,
    TimeProvider timeProvider,
    ILogger<AddCompanySupervisorCommandHandler> logger)
    : IRequestHandler<AddCompanySupervisorCommand, Result<CompanySupervisorDto>>
{
    public async Task<Result<CompanySupervisorDto>> Handle(AddCompanySupervisorCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var company = await context.Companies.FirstAsync(c => c.Id == companyId.Value, cancellationToken);

        // Roster checks before the account is created, so a rejected request leaves no account behind.
        var email = request.Email.Trim();
        var existing = await identityService.FindAccountByEmailAsync(email, cancellationToken);
        if (existing.IsSuccess)
        {
            if (existing.Value.Id == company.OwnerUserId) return CompanyErrors.OwnerCannotBeSupervisor;
            if (company.HasSupervisor(existing.Value.Id)) return CompanyErrors.SupervisorAlreadyAdded;
        }

        var account = await identityService.ProvisionAccountAsync(
            request.FullName, email, request.PhoneNumber, Roles.Supervisor, cancellationToken: cancellationToken);
        if (account.IsFailure)
        {
            return account.Error!;
        }

        var add = company.AddSupervisor(account.Value.Id, timeProvider.GetUtcNow());
        if (add.IsFailure)
        {
            return add.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);

        await emailSender.TrySendAsync(account.Value.Email,
            AccountEmails.For(account.Value, "supervisor", company.Name, appLinks.Value.DownloadUrl), logger, cancellationToken);

        var contact = await userDirectory.GetContactAsync(account.Value.Id, cancellationToken);
        return new CompanySupervisorDto(
            account.Value.Id, account.Value.FullName, account.Value.Email, contact?.PhoneNumber,
            add.Value.AddedAtUtc, GroupsCount: 0);
    }
}
