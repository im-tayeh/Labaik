using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Dtos;

// Kind 1 Text / 2 Location (latitude/longitude set; body = optional caption).
// IsRead is for the viewer: own messages are read; others are read once sent at or before the viewer's
// chat read marker. Realtime pushes (MessageReceived) always carry isRead = false.
public sealed record ChatMessageDto(
    Guid Id,
    Guid GroupId,
    Guid SenderId,
    string? SenderName,
    string Body,
    DateTimeOffset SentAtUtc,
    ChatMessageKind Kind,
    double? Latitude,
    double? Longitude,
    string? SenderPhotoUrl,
    bool IsRead);

internal static class ChatMessageMapper
{
    public static async Task<IReadOnlyList<ChatMessageDto>> ToDtosAsync(
        this IReadOnlyList<ChatMessage> messages,
        IAppDbContext context,
        IUserDirectory userDirectory,
        Guid? viewerId,
        DateTimeOffset? viewerLastReadUtc,
        CancellationToken cancellationToken)
    {
        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var names = await userDirectory.GetFullNamesAsync(senderIds, cancellationToken);
        var photos = await context.PilgrimProfiles.AsNoTracking()
            .Where(p => senderIds.Contains(p.UserId) && p.PhotoUrl != null)
            .ToDictionaryAsync(p => p.UserId, p => p.PhotoUrl, cancellationToken);

        return messages
            .Select(m => new ChatMessageDto(
                m.Id, m.GroupId, m.SenderId, names.GetValueOrDefault(m.SenderId), m.Body, m.SentAtUtc,
                m.Kind, m.Latitude, m.Longitude, photos.GetValueOrDefault(m.SenderId),
                IsRead: m.SenderId == viewerId || (viewerLastReadUtc is { } read && m.SentAtUtc <= read)))
            .ToList();
    }

    // Messages from others sent after the member's read marker.
    public static Task<int> CountUnreadChatAsync(
        this IAppDbContext context, Guid groupId, Guid userId, DateTimeOffset? lastReadUtc, CancellationToken cancellationToken)
        => context.ChatMessages.CountAsync(
            m => m.GroupId == groupId && m.SenderId != userId && (lastReadUtc == null || m.SentAtUtc > lastReadUtc),
            cancellationToken);
}
