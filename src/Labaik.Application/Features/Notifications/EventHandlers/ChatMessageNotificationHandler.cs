using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Chat.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// Group chat message -> notify every other member. From the group's supervisor: type 9
// SupervisorMessagePosted; from anyone else: type 8 GroupMessagePosted. referenceId = message id,
// preview = message text (null for a location without a caption).
public sealed class ChatMessageNotificationHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<ChatMessageSent>>
{
    public async Task Handle(DomainEventNotification<ChatMessageSent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == e.GroupId)
            .Select(g => new
            {
                g.LeaderId,
                Recipients = g.Members.Where(m => m.UserId != e.SenderId).Select(m => m.UserId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        var body = await context.ChatMessages
            .Where(m => m.Id == e.MessageId)
            .Select(m => m.Body)
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null || body is null)
        {
            return;
        }

        var type = group.LeaderId == e.SenderId
            ? NotificationType.SupervisorMessagePosted
            : NotificationType.GroupMessagePosted;

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, group.Recipients, type, e.GroupId, e.MessageId,
            body.Length == 0 ? null : body, timeProvider.GetUtcNow(), cancellationToken);
    }
}
