using Labaik.Domain.Common;

namespace Labaik.Domain.HelpCenter.Events;

public sealed record LostChildReported(Guid ReportId, Guid ReporterId) : IDomainEvent
{
    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
