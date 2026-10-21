using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Application.Features.DirectChat;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

internal sealed class SignalRRealtimeNotifier(
    IHubContext<EventsHub> hubContext,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
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

    public Task NotificationCreatedAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
        => SafeSendAsync("NotificationReceived", () =>
            hubContext.Clients.User(userId.ToString()).SendAsync("NotificationReceived", notification, cancellationToken));

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
