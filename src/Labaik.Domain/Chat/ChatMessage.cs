using Labaik.Domain.Chat.Events;
using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Chat;

// One message in a group's chat. Own aggregate (references GroupId) so nothing grows unbounded.
public sealed class ChatMessage : AggregateRoot
{
    public Guid GroupId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; }

    private ChatMessage() { } // EF

    private ChatMessage(Guid groupId, Guid senderId, string body, DateTimeOffset sentAtUtc)
    {
        GroupId = groupId;
        SenderId = senderId;
        Body = body;
        SentAtUtc = sentAtUtc;
        Raise(new ChatMessageSent(Id, groupId, senderId));
    }

    // Only Group.PostMessage creates messages (it enforces membership).
    internal static Result<ChatMessage> Create(Guid groupId, Guid senderId, string body, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return ChatErrors.BodyRequired;
        }

        body = body.Trim();
        if (body.Length > ChatErrors.BodyMaxLength)
        {
            return ChatErrors.BodyTooLong;
        }

        return new ChatMessage(groupId, senderId, body, sentAtUtc);
    }
}
