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

        var senderName = await userDirectory.GetFullNameAsync(message.SenderId, cancellationToken);
        await notifier.ChatMessageSentAsync(
            message.GroupId,
            new ChatMessageDto(message.Id, message.GroupId, message.SenderId, senderName, message.Body, message.SentAtUtc),
            cancellationToken);
    }
}
