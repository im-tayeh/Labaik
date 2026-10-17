using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// Gathering point set or moved -> notify every member except whoever changed it (preview = address).
public sealed class GatheringPointChangedHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<GatheringPointChanged>>
{
    public async Task Handle(DomainEventNotification<GatheringPointChanged> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == e.GroupId)
            .Select(g => new
            {
                Address = g.GatheringPoint == null ? null : g.GatheringPoint.Address,
                Recipients = g.Members.Where(m => m.UserId != e.ChangedBy).Select(m => m.UserId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return;
        }

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, group.Recipients, NotificationType.GatheringPointChanged,
            e.GroupId, e.GroupId, group.Address, timeProvider.GetUtcNow(), cancellationToken);
    }
}
