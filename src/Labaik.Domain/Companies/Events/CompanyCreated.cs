using Labaik.Domain.Common;

namespace Labaik.Domain.Companies.Events;

public sealed record CompanyCreated(Guid CompanyId, Guid OwnerUserId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
