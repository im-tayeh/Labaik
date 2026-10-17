using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies.Events;

namespace Labaik.Domain.Companies;

// An Umrah/Hajj operator. Created only by a platform admin; owned by one user account
// (the account holding the Company role). Owns trips and the groups under them.
public sealed class Company : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Company() { } // EF

    private Company(string name, Guid ownerUserId, DateTimeOffset createdAtUtc)
    {
        Name = name;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
        Raise(new CompanyCreated(Id, ownerUserId));
    }

    public static Result<Company> Create(string name, Guid ownerUserId, DateTimeOffset createdAtUtc)
    {
        var validName = ValidateName(name);
        if (validName.IsFailure)
        {
            return validName.Error!;
        }

        if (ownerUserId == Guid.Empty)
        {
            return CompanyErrors.OwnerRequired;
        }

        return new Company(validName.Value, ownerUserId, createdAtUtc);
    }

    public Result Rename(string name)
    {
        var validName = ValidateName(name);
        if (validName.IsFailure)
        {
            return Result.Failure(validName.Error!);
        }

        Name = validName.Value;
        return Result.Success();
    }

    private static Result<string> ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CompanyErrors.NameRequired;
        }

        name = name.Trim();
        return name.Length > CompanyErrors.NameMaxLength ? CompanyErrors.NameTooLong : name;
    }
}
