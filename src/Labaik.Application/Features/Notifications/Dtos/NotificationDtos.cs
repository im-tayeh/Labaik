using Labaik.Domain.Notifications;
using Labaik.Domain.Notifications.Enums;

namespace Labaik.Application.Features.Notifications.Dtos;

// Type drives the localized text on the client; Preview is the announcement title or the new member's name.
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? GroupId,
    Guid? ReferenceId,
    string? Preview,
    DateTimeOffset CreatedAtUtc,
    bool IsRead);

public sealed record UnreadCountDto(int Count);

public static class NotificationMapper
{
    public static NotificationDto ToDto(this Notification n) => new(
        n.Id, n.Type, n.GroupId, n.ReferenceId, n.Preview, n.CreatedAtUtc, n.IsRead);
}
