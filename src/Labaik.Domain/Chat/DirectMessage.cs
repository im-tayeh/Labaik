using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Chat;

public sealed class DirectMessage : AggregateRoot
{
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; }

    private DirectMessage() { }

    private DirectMessage(Guid conversationId, Guid senderId, string body, DateTimeOffset nowUtc)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body;
        SentAtUtc = nowUtc;
    }

    internal static Result<DirectMessage> Create(Guid conversationId, Guid senderId, string body, DateTimeOffset nowUtc)
    {
        if (conversationId == Guid.Empty || senderId == Guid.Empty) return Error.Validation("Chat.InvalidParticipants", "A conversation and sender are required.");
        return new DirectMessage(conversationId, senderId, body, nowUtc);
    }
}
