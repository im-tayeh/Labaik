using FluentValidation;
using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Application.Features.Admin.Commands.CreateCompany;

// Admin only (endpoint policy). Creates the company and its owner account (Company role):
// a new email gets a confirmed account + emailed temporary password; an already-registered
// email is linked as owner and gets a notice without a password. The contact person's name
// and mobile become the new owner account's name and phone.
public sealed record CreateCompanyCommand : CompanyFields, IRequest<Result<AdminCompanyDto>>
{
    public string OwnerEmail { get; init; } = string.Empty;
}

public sealed class CreateCompanyCommandValidator : CompanyFieldsValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(CompanyErrors.EmailMaxLength);
    }
}

public sealed class CreateCompanyCommandHandler(
    IAppDbContext context,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<CreateCompanyCommandHandler> logger) : IRequestHandler<CreateCompanyCommand, Result<AdminCompanyDto>>
{
    public async Task<Result<AdminCompanyDto>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        // Every check before the account is created, so a rejected request leaves no account behind.
        var registration = request.CommercialRegistrationNumber.Trim();
        if (await context.Companies.AnyAsync(c => c.CommercialRegistrationNumber == registration, cancellationToken))
        {
            return CompanyErrors.RegistrationNumberTaken;
        }

        var ownerEmail = request.OwnerEmail.Trim();
        var existing = await identityService.FindAccountByEmailAsync(ownerEmail, cancellationToken);
        if (existing.IsSuccess)
        {
            var existingId = existing.Value.Id;
            if (await context.Companies.AnyAsync(c => c.OwnerUserId == existingId, cancellationToken))
            {
                return CompanyErrors.OwnerAlreadyHasCompany;
            }
        }

        var owner = await identityService.ProvisionAccountAsync(
            request.ContactFullName, ownerEmail, request.ContactMobile, Roles.Company, cancellationToken);
        if (owner.IsFailure)
        {
            return owner.Error!;
        }

        var create = Company.Create(request.ToDetails(), owner.Value.Id, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        var company = create.Value;
        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);

        await emailSender.TrySendAsync(owner.Value.Email,
            AccountEmails.For(owner.Value, "company", company.Name), logger, cancellationToken);

        return await company.ToAdminDtoAsync(context, userDirectory, timeProvider.TodayUtc(), cancellationToken);
    }
}
