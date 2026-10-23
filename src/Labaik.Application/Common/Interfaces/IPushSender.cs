using Labaik.Domain.Notifications.Enums;

namespace Labaik.Application.Common.Interfaces;

// One call per notification fan-out (same type/reference/preview for every recipient), so a group-wide
// notification costs one device-token query and one FCM call per 500 devices, not one of each per user.
public interface IPushSender
{
    Task SendAsync(IReadOnlyCollection<Guid> userIds, NotificationType type, Guid? referenceId, string? preview,
        CancellationToken cancellationToken = default);
}

public sealed class NoopPushSender : IPushSender
{
    public Task SendAsync(IReadOnlyCollection<Guid> userIds, NotificationType type, Guid? referenceId, string? preview,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
