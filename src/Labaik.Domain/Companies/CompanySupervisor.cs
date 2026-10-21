using Labaik.Domain.Common;

namespace Labaik.Domain.Companies;

// A supervisor account on a company's roster. Only rostered supervisors can be assigned to its groups.
public sealed class CompanySupervisor : Entity
{
    public Guid UserId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
    public Guid? ProvisionedByCompanyId { get; private set; }
    public bool IsSuspended { get; private set; }

    private CompanySupervisor() { } // EF

    internal CompanySupervisor(Guid userId, DateTimeOffset addedAtUtc, Guid? provisionedByCompanyId)
    {
        UserId = userId;
        AddedAtUtc = addedAtUtc;
        ProvisionedByCompanyId = provisionedByCompanyId;
    }

    internal void Suspend() => IsSuspended = true;
    internal void Reactivate() => IsSuspended = false;
}
