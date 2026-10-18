using Labaik.Domain.Chat.Events;
using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Chat;

public enum ChatMessageKind { Text = 1, Location = 2 }

// What a member sends: text (body required) or a shared location (coordinates required, body = optional caption).
public sealed record ChatMessageContent(ChatMessageKind Kind, string? Body, double? Latitude = null, double? Longitude = null)
{
    public static ChatMessageContent Text(string body) => new(ChatMessageKind.Text, body);
}

// One message in a group's chat. Own aggregate (references GroupId) so nothing grows unbounded.
public sealed class ChatMessage : AggregateRoot
{
    public Guid GroupId { get; private set; }
    public Guid SenderId { get; private set; }
    public ChatMessageKind Kind { get; private set; } = ChatMessageKind.Text;
    public string Body { get; private set; } = string.Empty; // empty for a location without a caption
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private ChatMessage() { } // EF

    private ChatMessage(Guid groupId, Guid senderId, ChatMessageKind kind, string body, double? latitude, double? longitude,
        DateTimeOffset sentAtUtc)
    {
        GroupId = groupId;
        SenderId = senderId;
        Kind = kind;
        Body = body;
        Latitude = latitude;
        Longitude = longitude;
        SentAtUtc = sentAtUtc;
        Raise(new ChatMessageSent(Id, groupId, senderId));
    }

    // Only Group.PostMessage creates messages (it enforces membership).
    internal static Result<ChatMessage> Create(Guid groupId, Guid senderId, ChatMessageContent content, DateTimeOffset sentAtUtc)
    {
        var body = content.Body?.Trim() ?? string.Empty;
        if (body.Length > ChatErrors.BodyMaxLength)
        {
            return ChatErrors.BodyTooLong;
        }

        switch (content.Kind)
        {
            case ChatMessageKind.Text:
                if (body.Length == 0) return ChatErrors.BodyRequired;
                return new ChatMessage(groupId, senderId, ChatMessageKind.Text, body, null, null, sentAtUtc);

            case ChatMessageKind.Location:
                if (content.Latitude is not { } lat || content.Longitude is not { } lng
                    || lat is < -90 or > 90 || lng is < -180 or > 180)
                {
                    return ChatErrors.InvalidLocation;
                }

                return new ChatMessage(groupId, senderId, ChatMessageKind.Location, body, lat, lng, sentAtUtc);

            default:
                return ChatErrors.InvalidKind;
        }
    }
}
