using FluentValidation;
using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Commands.CreateCompany;

// Admin only (endpoint policy). The owner must already have an account (registered normally);
// this links it to a new company and grants it the Company role (effective from its next token).
public sealed record CreateCompanyCommand : CompanyFields, IRequest<Result<AdminCompanyDto>>
{
    public string OwnerEmail { get; init; } = string.Empty;
}

public sealed class CreateCompanyCommandValidator : CompanyFieldsValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress();
    }
}

public sealed class CreateCompanyCommandHandler(
    IAppDbContext context,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<CreateCompanyCommand, Result<AdminCompanyDto>>
{
    public async Task<Result<AdminCompanyDto>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var registration = request.CommercialRegistrationNumber.Trim();
        if (await context.Companies.AnyAsync(c => c.CommercialRegistrationNumber == registration, cancellationToken))
        {
            return CompanyErrors.RegistrationNumberTaken;
        }

        var owner = await identityService.FindAccountByEmailAsync(request.OwnerEmail.Trim(), cancellationToken);
        if (owner.IsFailure)
        {
            return owner.Error!;
        }

        var ownerId = owner.Value.Id;
        if (await context.Companies.AnyAsync(c => c.OwnerUserId == ownerId, cancellationToken))
        {
            return CompanyErrors.OwnerAlreadyHasCompany;
        }

        var create = Company.Create(request.ToDetails(), ownerId, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        // Role first: idempotent, so a retry after a failed save is safe.
        var role = await identityService.AddToRoleAsync(ownerId, Roles.Company, cancellationToken);
        if (role.IsFailure)
        {
            return role.Error!;
        }

        var company = create.Value;
        context.Companies.Add(company);
        await context.SaveChangesAsync(cancellationToken);

        return await company.ToAdminDtoAsync(context, userDirectory, timeProvider.TodayUtc(), cancellationToken);
    }
}
