using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Chat;

public sealed class Conversation : AggregateRoot
{
    public Guid ParticipantAId { get; private set; }
    public Guid ParticipantBId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastMessageAtUtc { get; private set; }
    public DateTimeOffset? ALastReadAtUtc { get; private set; }
    public DateTimeOffset? BLastReadAtUtc { get; private set; }

    private Conversation() { }

    private Conversation(Guid a, Guid b, DateTimeOffset nowUtc)
    {
        ParticipantAId = a;
        ParticipantBId = b;
        CreatedAtUtc = nowUtc;
    }

    public static Result<Conversation> Start(Guid userId, Guid otherId, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty || otherId == Guid.Empty || userId == otherId)
            return Error.Validation("Chat.InvalidParticipants", "Two different participants are required.");
        var (a, b) = userId.CompareTo(otherId) < 0 ? (userId, otherId) : (otherId, userId);
        return new Conversation(a, b, nowUtc);
    }

    public bool Has(Guid userId) => userId == ParticipantAId || userId == ParticipantBId;
    public Guid OtherThan(Guid userId) => userId == ParticipantAId ? ParticipantBId : ParticipantAId;

    public Result<DirectMessage> Post(Guid senderId, string body, DateTimeOffset nowUtc)
    {
        if (!Has(senderId)) return Error.NotFound("Chat.ConversationNotFound", "The conversation was not found.");
        body = body?.Trim() ?? string.Empty;
        if (body.Length == 0) return ChatErrors.BodyRequired;
        if (body.Length > ChatErrors.BodyMaxLength) return ChatErrors.BodyTooLong;
        LastMessageAtUtc = nowUtc;
        return DirectMessage.Create(Id, senderId, body, nowUtc);
    }

    public Result MarkRead(Guid userId, DateTimeOffset nowUtc)
    {
        if (!Has(userId)) return Result.Failure(Error.NotFound("Chat.ConversationNotFound", "The conversation was not found."));
        if (userId == ParticipantAId)
        {
            if (ALastReadAtUtc is null || nowUtc > ALastReadAtUtc) ALastReadAtUtc = nowUtc;
        }
        else if (BLastReadAtUtc is null || nowUtc > BLastReadAtUtc) BLastReadAtUtc = nowUtc;
        return Result.Success();
    }

    public DateTimeOffset? LastReadBy(Guid userId) => userId == ParticipantAId ? ALastReadAtUtc : BLastReadAtUtc;
}
