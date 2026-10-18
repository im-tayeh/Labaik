using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Domain.Chat.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.EventHandlers;

// Pushes a saved message to the group's connected members.
public sealed class ChatMessageSentHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier) : INotificationHandler<DomainEventNotification<ChatMessageSent>>
{
    public async Task Handle(DomainEventNotification<ChatMessageSent> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var message = await context.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == e.MessageId, cancellationToken);
        if (message is null)
        {
            return;
        }

        var dtos = await new[] { message }.ToDtosAsync(context, userDirectory, viewerId: null, viewerLastReadUtc: null, cancellationToken);
        await notifier.ChatMessageSentAsync(message.GroupId, dtos[0], cancellationToken);
    }
}
