using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// A pilgrim confirmed arrival -> notify the group's supervisor (preview = pilgrim name, referenceId = pilgrim id).
public sealed class PilgrimArrivedHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<PilgrimArrived>>
{
    public async Task Handle(DomainEventNotification<PilgrimArrived> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        var leaderId = await context.Groups
            .Where(g => g.Id == e.GroupId)
            .Select(g => g.LeaderId)
            .FirstOrDefaultAsync(cancellationToken);
        if (leaderId is null)
        {
            return;
        }

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, [leaderId.Value], NotificationType.PilgrimArrived, e.GroupId, e.UserId,
            await userDirectory.GetFullNameAsync(e.UserId, cancellationToken), timeProvider.GetUtcNow(), cancellationToken);
    }
}
