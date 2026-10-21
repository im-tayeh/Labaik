using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Notifications.Dtos;

namespace Labaik.Application.Common.Interfaces;

// Pushes chat messages and notifications to connected clients (SignalR in the Api layer).
// Implementations must not throw: a failed push must never fail the saved change.
public interface IRealtimeNotifier
{
    Task ChatMessageSentAsync(Guid groupId, ChatMessageDto message, CancellationToken cancellationToken = default);
    Task ChatMessageDeletedAsync(Guid groupId, Guid messageId, CancellationToken cancellationToken = default);

    Task NotificationCreatedAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
