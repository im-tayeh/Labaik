using Labaik.Domain.Chat;

namespace Labaik.Domain.Tests;

public class DirectChatTests
{
    [Fact]
    public void Conversation_orders_participants_and_tracks_messages_and_read_markers()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var first = Conversation.Start(a, b, now).Value;
        var reversed = Conversation.Start(b, a, now).Value;
        Assert.Equal(first.ParticipantAId, reversed.ParticipantAId);
        Assert.Equal(first.ParticipantBId, reversed.ParticipantBId);
        Assert.True(first.Post(a, "  hello  ", now).IsSuccess);
        Assert.Equal("Chat.BodyRequired", first.Post(a, " ", now).Error!.Code);
        Assert.Equal("Chat.ConversationNotFound", first.Post(Guid.NewGuid(), "hello", now).Error!.Code);
        Assert.True(first.MarkRead(b, now).IsSuccess);
        Assert.True(first.MarkRead(b, now.AddMinutes(-1)).IsSuccess);
        Assert.Equal(now, first.LastReadBy(b));
    }
}
