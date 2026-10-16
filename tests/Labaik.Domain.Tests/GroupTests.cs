using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;

namespace Labaik.Domain.Tests;

public class GroupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid LeaderId = Guid.NewGuid();

    private static Group NewGroup() => Group.Create("  Caravan ", " ab12cd ", LeaderId, Now).Value;

    [Fact]
    public void Create_normalizes_and_makes_creator_the_leader()
    {
        var group = NewGroup();

        Assert.Equal("Caravan", group.Name);
        Assert.Equal("AB12CD", group.JoinCode);
        var leader = Assert.Single(group.Members);
        Assert.Equal(GroupMemberRole.Leader, leader.Role);
        Assert.True(group.IsMember(LeaderId));
    }

    [Theory]
    [InlineData("", "CODE", "Group.NameRequired")]
    [InlineData("Name", " ", "Group.JoinCodeRequired")]
    public void Create_validates_input(string name, string code, string expectedError)
    {
        Assert.Equal(expectedError, Group.Create(name, code, LeaderId, Now).Error!.Code);
    }

    [Fact]
    public void Join_adds_member_once()
    {
        var group = NewGroup();
        var userId = Guid.NewGuid();

        Assert.True(group.Join(userId, Now).IsSuccess);
        Assert.Equal("Group.AlreadyMember", group.Join(userId, Now).Error!.Code);
        Assert.Equal(2, group.MemberCount);
    }

    [Fact]
    public void Only_the_leader_can_post_announcements()
    {
        var group = NewGroup();
        var memberId = Guid.NewGuid();
        group.Join(memberId, Now);

        Assert.Equal("Group.NotLeader", group.PostAnnouncement(memberId, "T", "B", Now).Error!.Code);

        var post = group.PostAnnouncement(LeaderId, " Bus at 6 ", "Lobby", Now);
        Assert.True(post.IsSuccess);
        Assert.Equal("Bus at 6", post.Value.Title);
        Assert.Equal(group.Id, post.Value.GroupId);
    }

    [Fact]
    public void Only_members_can_post_chat_messages()
    {
        var group = NewGroup();

        Assert.Equal("Chat.NotMember", group.PostMessage(Guid.NewGuid(), "hi", Now).Error!.Code);
        Assert.Equal("Chat.BodyRequired", group.PostMessage(LeaderId, "   ", Now).Error!.Code);
        Assert.Equal("Chat.BodyTooLong", group.PostMessage(LeaderId, new string('x', 2001), Now).Error!.Code);
        Assert.True(group.PostMessage(LeaderId, "hi", Now).IsSuccess);
    }
}
