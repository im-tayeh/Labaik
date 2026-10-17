using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Companies;

public static class CompanyErrors
{
    public const int NameMaxLength = 150;

    public static Error NameRequired =>
        Error.Validation("Company.NameRequired", "Company name is required.");

    public static Error NameTooLong =>
        Error.Validation("Company.NameTooLong", $"Company name must be at most {NameMaxLength} characters.");

    public static Error OwnerRequired =>
        Error.Validation("Company.OwnerRequired", "A company must have an owner account.");

    public static Error OwnerAlreadyHasCompany =>
        Error.Conflict("Company.OwnerAlreadyHasCompany", "This account already owns a company.");

    public static Error NotFound =>
        Error.NotFound("Company.NotFound", "No company found for the current account.");
}
