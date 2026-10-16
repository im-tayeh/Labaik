using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Notifications.Enums;

namespace Labaik.Domain.Notifications;

// An in-app notification for one user. Stores structured data (type, ids, a short preview such as
// an announcement title or a member's name), never a composed sentence: Flutter localizes the text.
public sealed class Notification : AggregateRoot
{
    public const int PreviewMaxLength = 200;

    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Preview { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    private Notification() { } // EF

    private Notification(
        Guid userId, NotificationType type, Guid? groupId, Guid? referenceId, string? preview, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        Type = type;
        GroupId = groupId;
        ReferenceId = referenceId;
        Preview = preview;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<Notification> Create(
        Guid userId, NotificationType type, Guid? groupId, Guid? referenceId, string? preview, DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return Error.Validation("Notification.UserRequired", "A notification must have a recipient.");
        }

        preview = string.IsNullOrWhiteSpace(preview) ? null : preview.Trim();
        if (preview is { Length: > PreviewMaxLength })
        {
            preview = preview[..PreviewMaxLength];
        }

        return new Notification(userId, type, groupId, referenceId, preview, createdAtUtc);
    }

    // Idempotent.
    public void MarkRead(DateTimeOffset readAtUtc) => ReadAtUtc ??= readAtUtc;
}
