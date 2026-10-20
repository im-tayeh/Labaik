using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Companies;

public static class CompanyErrors
{
    public const int NameMaxLength = 150;
    public const int RegistrationNumberMaxLength = 50;
    public const int ContactNameMaxLength = 150;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 30;
    public const int PlaceMaxLength = 100;   // country, city
    public const int ReferenceMaxLength = 50; // VAT id, contract reference
    public const int JobTitleMaxLength = 100;
    public const int UrlMaxLength = 2048;

    public static Error NameRequired =>
        Error.Validation("Company.NameRequired", "Company name is required.");

    public static Error NameTooLong =>
        Error.Validation("Company.NameTooLong", $"Company name must be at most {NameMaxLength} characters.");

    public static Error RegistrationNumberRequired =>
        Error.Validation("Company.RegistrationNumberRequired", "Commercial registration number is required.");

    public static Error ContactNameRequired =>
        Error.Validation("Company.ContactNameRequired", "Contact person's full name is required.");

    public static Error TooLong =>
        Error.Validation("Company.TooLong", "One or more company fields are too long.");

    public static Error InvalidUrl =>
        Error.Validation("Company.InvalidUrl", "Logo and contract file URLs must be absolute https URLs.");

    public static Error NegativeNumber =>
        Error.Validation("Company.NegativeNumber", "Price and capacity limits cannot be negative.");

    public static Error ContractEndBeforeStart =>
        Error.Validation("Company.ContractEndBeforeStart", "Contract end date cannot be before its start date.");

    public static Error OwnerRequired =>
        Error.Validation("Company.OwnerRequired", "A company must have an owner account.");

    public static Error OwnerAlreadyHasCompany =>
        Error.Conflict("Company.OwnerAlreadyHasCompany", "This account already owns a company.");

    public static Error RegistrationNumberTaken =>
        Error.Conflict("Company.RegistrationNumberTaken", "A company with this commercial registration number already exists.");

    public static Error NotFound =>
        Error.NotFound("Company.NotFound", "No company found for the current account.");

    public static Error SupervisorRequired =>
        Error.Validation("Company.SupervisorRequired", "A supervisor account is required.");

    public static Error OwnerCannotBeSupervisor =>
        Error.Conflict("Company.OwnerCannotBeSupervisor", "The company's owner account cannot be one of its supervisors.");

    public static Error SupervisorAlreadyAdded =>
        Error.Conflict("Company.SupervisorAlreadyAdded", "This account is already one of the company's supervisors.");

    public static Error SupervisorNotOnRoster =>
        Error.NotFound("Company.SupervisorNotOnRoster", "This account is not one of the company's supervisors.");

    public static Error SupervisorLeadsGroups =>
        Error.Conflict("Company.SupervisorLeadsGroups", "The supervisor still leads groups of this company. Reassign them first.");

    public static Error SupervisorNotFound =>
        Error.NotFound("Company.SupervisorNotFound", "No supervisor with this email on the company's roster.");

    public static Error NotFoundById =>
        Error.NotFound("Company.NotFound", "Company not found.");
}
