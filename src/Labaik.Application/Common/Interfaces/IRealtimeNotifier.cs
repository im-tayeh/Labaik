using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Application.Features.DirectChat;

namespace Labaik.Application.Common.Interfaces;

// Pushes chat messages and notifications to connected clients (SignalR in the Api layer).
// Implementations must not throw: a failed push must never fail the saved change.
public interface IRealtimeNotifier
{
    Task ChatMessageSentAsync(Guid groupId, ChatMessageDto message, CancellationToken cancellationToken = default);
    Task ChatMessageDeletedAsync(Guid groupId, Guid messageId, CancellationToken cancellationToken = default);
    Task DirectMessageReceivedAsync(Guid recipientId, DirectMessageDto message, CancellationToken cancellationToken = default);

    // The notifications of one fan-out (one per recipient): each user gets theirs live, then one push for all.
    Task NotificationsCreatedAsync(IReadOnlyList<(Guid UserId, NotificationDto Notification)> notifications,
        CancellationToken cancellationToken = default);

    // The user's account was just blocked (code = Company.Stopped, Supervisor.Suspended, Auth.AccountDisabled):
    // signed-in apps sign out right away instead of on their next request.
    Task AccountBlockedAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
