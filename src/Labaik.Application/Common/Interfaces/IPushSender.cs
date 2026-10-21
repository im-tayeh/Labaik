using Labaik.Domain.Notifications.Enums;

namespace Labaik.Application.Common.Interfaces;

public interface IPushSender
{
    Task SendAsync(Guid userId, NotificationType type, Guid? referenceId, string? preview,
        CancellationToken cancellationToken = default);
}

public sealed class NoopPushSender : IPushSender
{
    public Task SendAsync(Guid userId, NotificationType type, Guid? referenceId, string? preview,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
