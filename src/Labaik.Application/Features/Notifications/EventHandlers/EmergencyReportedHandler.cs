using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Map.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// SOS -> the supervisors of every group the pilgrim belongs to (referenceId = alert id, preview = pilgrim name).
public sealed class EmergencyReportedHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<EmergencyReported>>
{
    public async Task Handle(DomainEventNotification<EmergencyReported> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var supervisors = await context.GetSupervisorIdsOfAsync(e.UserId, cancellationToken);
        if (supervisors.Count == 0)
        {
            return;
        }

        var name = await userDirectory.GetFullNameAsync(e.UserId, cancellationToken);
        await NotificationWriter.CreateAndPushAsync(
            context, notifier, supervisors, NotificationType.EmergencyReported,
            groupId: null, e.AlertId, name, timeProvider.GetUtcNow(), cancellationToken);
    }
}
