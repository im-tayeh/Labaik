using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Commands.UpdateMyCompany;

// The company's own "edit company data" screen: a subset of the admin form (contract, limits and
// services stay admin-only). Full replace of these fields.
public sealed record UpdateMyCompanyCommand(
    string Name,
    string CommercialRegistrationNumber,
    string ContactFullName,
    string? LogoUrl,
    string? Country,
    string? City,
    string? OfficialEmail,
    string? PrimaryPhone) : IRequest<Result<CompanyDto>>;

public sealed class UpdateMyCompanyCommandValidator : AbstractValidator<UpdateMyCompanyCommand>
{
    public UpdateMyCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CompanyErrors.NameMaxLength);
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(CompanyErrors.RegistrationNumberMaxLength);
        RuleFor(x => x.ContactFullName).NotEmpty().MaximumLength(CompanyErrors.ContactNameMaxLength);
        RuleFor(x => x.LogoUrl).Must(url => string.IsNullOrWhiteSpace(url)
                || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Must be an absolute https URL.")
            .MaximumLength(CompanyErrors.UrlMaxLength);
        RuleFor(x => x.Country).MaximumLength(CompanyErrors.PlaceMaxLength);
        RuleFor(x => x.City).MaximumLength(CompanyErrors.PlaceMaxLength);
        RuleFor(x => x.OfficialEmail).EmailAddress().MaximumLength(CompanyErrors.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.OfficialEmail));
        RuleFor(x => x.PrimaryPhone).MaximumLength(CompanyErrors.PhoneMaxLength);
    }
}

public sealed class UpdateMyCompanyCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<UpdateMyCompanyCommand, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(UpdateMyCompanyCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var registration = request.CommercialRegistrationNumber.Trim();
        if (await context.Companies.AnyAsync(
                c => c.Id != companyId.Value && c.CommercialRegistrationNumber == registration, cancellationToken))
        {
            return CompanyErrors.RegistrationNumberTaken;
        }

        var company = await context.Companies.FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        var update = company.UpdateProfile(new CompanyProfile(
            request.Name, request.CommercialRegistrationNumber, request.ContactFullName,
            request.LogoUrl, request.Country, request.City, request.OfficialEmail, request.PrimaryPhone));
        if (update.IsFailure)
        {
            return update.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return company.ToDto();
    }
}
