using FluentValidation;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Companies;
using Labaik.Domain.Companies.Enums;

namespace Labaik.Application.Features.Companies.Common;

// The admin "create / edit company" form fields (same flat JSON shape for both).
// Required: name, commercialRegistrationNumber, contactFullName. Everything else is optional.
public abstract record CompanyFields
{
    public string Name { get; init; } = string.Empty;
    public string CommercialRegistrationNumber { get; init; } = string.Empty;
    public CompanyCategory? Category { get; init; }
    public string? OfficialEmail { get; init; }
    public string? PrimaryPhone { get; init; }
    public string? LogoUrl { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }

    public string? VatId { get; init; }
    public string? ContractReferenceNumber { get; init; }
    public decimal? SubscriptionPrice { get; init; }
    public SubscriptionPeriod? SubscriptionPeriod { get; init; }
    public DateOnly? ContractStartDate { get; init; }
    public DateOnly? ContractEndDate { get; init; }
    public int? MaxSupervisors { get; init; }
    public int? MaxPilgrims { get; init; }
    public string? ContractFileUrl { get; init; }

    public string ContactFullName { get; init; } = string.Empty;
    public string? ContactJobTitle { get; init; }
    public string? ContactMobile { get; init; }

    // Omitted -> every service off.
    public CompanyServicesDto? EnabledServices { get; init; }

    public CompanyDetails ToDetails() => new(
        Name, CommercialRegistrationNumber, ContactFullName,
        Category, OfficialEmail, PrimaryPhone, LogoUrl, Country, City,
        VatId, ContractReferenceNumber, SubscriptionPrice, SubscriptionPeriod, ContractStartDate, ContractEndDate,
        MaxSupervisors, MaxPilgrims, ContractFileUrl, ContactJobTitle, ContactMobile,
        EnabledServices?.ToDomain());
}

public abstract class CompanyFieldsValidator<T> : AbstractValidator<T> where T : CompanyFields
{
    protected CompanyFieldsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CompanyErrors.NameMaxLength);
        RuleFor(x => x.CommercialRegistrationNumber).NotEmpty().MaximumLength(CompanyErrors.RegistrationNumberMaxLength);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.OfficialEmail).EmailAddress().MaximumLength(CompanyErrors.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.OfficialEmail));
        RuleFor(x => x.PrimaryPhone).MaximumLength(CompanyErrors.PhoneMaxLength);
        RuleFor(x => x.LogoUrl).Must(BeHttpsUrl).WithMessage("Must be an absolute https URL.")
            .MaximumLength(CompanyErrors.UrlMaxLength);
        RuleFor(x => x.Country).MaximumLength(CompanyErrors.PlaceMaxLength);
        RuleFor(x => x.City).MaximumLength(CompanyErrors.PlaceMaxLength);

        RuleFor(x => x.VatId).MaximumLength(CompanyErrors.ReferenceMaxLength);
        RuleFor(x => x.ContractReferenceNumber).MaximumLength(CompanyErrors.ReferenceMaxLength);
        RuleFor(x => x.SubscriptionPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SubscriptionPeriod).IsInEnum();
        RuleFor(x => x.ContractEndDate).GreaterThanOrEqualTo(x => x.ContractStartDate)
            .When(x => x.ContractStartDate is not null && x.ContractEndDate is not null);
        RuleFor(x => x.MaxSupervisors).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPilgrims).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContractFileUrl).Must(BeHttpsUrl).WithMessage("Must be an absolute https URL.")
            .MaximumLength(CompanyErrors.UrlMaxLength);

        RuleFor(x => x.ContactFullName).NotEmpty().MaximumLength(CompanyErrors.ContactNameMaxLength);
        RuleFor(x => x.ContactJobTitle).MaximumLength(CompanyErrors.JobTitleMaxLength);
        RuleFor(x => x.ContactMobile).MaximumLength(CompanyErrors.PhoneMaxLength);
    }

    internal static bool BeHttpsUrl(string? url) => string.IsNullOrWhiteSpace(url)
        || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
