using Labaik.Domain.Common;

namespace Labaik.Domain.Companies;

// A supervisor account on a company's roster. Only rostered supervisors can be assigned to its groups.
// A suspended entry keeps the roster row; a supervisor suspended by every company they are on cannot sign in.
public sealed class CompanySupervisor : Entity
{
    public Guid UserId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
    public bool IsSuspended { get; private set; }

    private CompanySupervisor() { } // EF

    internal CompanySupervisor(Guid userId, DateTimeOffset addedAtUtc)
    {
        UserId = userId;
        AddedAtUtc = addedAtUtc;
    }

    internal void Suspend() => IsSuspended = true;

    internal void Reactivate() => IsSuspended = false;
}
