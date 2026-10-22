using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Application.Features.DirectChat;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

internal sealed class SignalRRealtimeNotifier(
    IHubContext<EventsHub> hubContext,
    ILogger<SignalRRealtimeNotifier> logger,
    IPushSender pushSender) : IRealtimeNotifier
{
    public Task ChatMessageSentAsync(Guid groupId, ChatMessageDto message, CancellationToken cancellationToken = default)
        => SafeSendAsync("MessageReceived", () =>
            hubContext.Clients.Group(GroupSubscribedHub.GroupName(groupId)).SendAsync("MessageReceived", message, cancellationToken));

    public Task ChatMessageDeletedAsync(Guid groupId, Guid messageId, CancellationToken cancellationToken = default)
        => SafeSendAsync("MessageDeleted", () =>
            hubContext.Clients.Group(GroupSubscribedHub.GroupName(groupId)).SendAsync("MessageDeleted", messageId, cancellationToken));

    public Task DirectMessageReceivedAsync(Guid recipientId, DirectMessageDto message, CancellationToken cancellationToken = default)
        => SafeSendAsync("DirectMessageReceived", () =>
            hubContext.Clients.User(recipientId.ToString()).SendAsync("DirectMessageReceived", message, cancellationToken));

    public async Task NotificationsCreatedAsync(IReadOnlyList<(Guid UserId, NotificationDto Notification)> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0) return;
        foreach (var (userId, notification) in notifications)
        {
            await SafeSendAsync("NotificationReceived", () =>
                hubContext.Clients.User(userId.ToString()).SendAsync("NotificationReceived", notification, cancellationToken));
        }
        var first = notifications[0].Notification;
        try
        {
            await pushSender.SendAsync(notifications.Select(n => n.UserId).ToList(), first.Type, first.ReferenceId, first.Preview, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push notification delivery failed for {Count} users.", notifications.Count);
        }
    }

    public Task AccountBlockedAsync(Guid userId, string code, CancellationToken cancellationToken = default)
        => SafeSendAsync("AccountBlocked", () =>
            hubContext.Clients.User(userId.ToString()).SendAsync("AccountBlocked", new { code }, cancellationToken));

    // Best effort: the change is already saved, so a push failure is logged, never thrown.
    private async Task SafeSendAsync(string method, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push {Method} failed.", method);
        }
    }
}
