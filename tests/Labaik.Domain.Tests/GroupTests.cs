using Labaik.Domain.Announcements;
using Labaik.Domain.Chat;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Groups.Events;

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

        Assert.Equal("Group.NotLeader", group.PostAnnouncement(memberId, new AnnouncementContent("T", "B"), Now).Error!.Code);

        var post = group.PostAnnouncement(LeaderId, new AnnouncementContent(" Bus at 6 ", "Lobby"), Now);
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

    [Fact]
    public void Location_messages_need_valid_coordinates_and_allow_an_empty_caption()
    {
        var group = NewGroup();

        var ok = group.PostMessage(LeaderId, new ChatMessageContent(ChatMessageKind.Location, null, 21.4225, 39.8262), Now).Value;
        Assert.Equal(ChatMessageKind.Location, ok.Kind);
        Assert.Equal(string.Empty, ok.Body);
        Assert.Equal("Chat.InvalidLocation",
            group.PostMessage(LeaderId, new ChatMessageContent(ChatMessageKind.Location, "here", 95, 39), Now).Error!.Code);
        Assert.Equal("Chat.InvalidLocation",
            group.PostMessage(LeaderId, new ChatMessageContent(ChatMessageKind.Location, "here"), Now).Error!.Code);
    }

    [Fact]
    public void Chat_read_marker_only_moves_forward_and_is_for_members()
    {
        var group = NewGroup();

        Assert.True(group.MarkChatRead(LeaderId, Now).IsSuccess);
        Assert.True(group.MarkChatRead(LeaderId, Now.AddHours(-1)).IsSuccess);
        Assert.Equal(Now, group.Members.Single(m => m.UserId == LeaderId).ChatLastReadAtUtc);
        Assert.Equal("Chat.NotMember", group.MarkChatRead(Guid.NewGuid(), Now).Error!.Code);
    }

    [Fact]
    public void Supervisor_controls_group_chat_and_can_delete_messages()
    {
        var group = NewGroup();
        var memberId = Guid.NewGuid();
        group.Join(memberId, Now);
        var message = group.PostMessage(memberId, "hello", Now).Value;

        Assert.True(group.SetChatSettings(LeaderId, true).IsSuccess);
        Assert.Equal("Chat.SupervisorOnly", group.PostMessage(memberId, "blocked", Now).Error!.Code);
        Assert.True(group.SetChatSettings(LeaderId, false).IsSuccess);
        Assert.True(group.MuteChatMember(LeaderId, memberId, 30, Now).IsSuccess);
        Assert.Equal("Chat.Muted", group.PostMessage(memberId, "muted", Now.AddMinutes(1)).Error!.Code);
        Assert.Equal("Chat.InvalidMuteDuration", group.MuteChatMember(LeaderId, memberId, 10081, Now).Error!.Code);
        Assert.Equal("Group.NotLeader", group.DeleteMessage(Guid.NewGuid(), message, Now).Error!.Code);
        Assert.True(group.DeleteMessage(LeaderId, message, Now).IsSuccess);
        Assert.NotNull(message.DeletedAtUtc);
    }
}

public class CompanyGroupDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static Group NewTripGroup() =>
        Group.CreateForTrip("Group A", "CODE01", Guid.NewGuid(), Guid.NewGuid(), Now).Value;

    [Fact]
    public void Capacity_limits_pilgrims_and_cannot_be_lowered_below_existing_count()
    {
        var group = NewTripGroup();
        Assert.True(group.UpdateDetails("Group A", null, 1).IsSuccess);
        Assert.True(group.AddMember(Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal("Group.CapacityReached", group.AddMember(Guid.NewGuid(), Now).Error!.Code);
        Assert.True(group.UpdateDetails("Group A", null, 2).IsSuccess);
        Assert.True(group.AddMember(Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal("Group.CapacityBelowMembers", group.UpdateDetails("Group A", null, 1).Error!.Code);
    }

    [Fact]
    public void CreateForTrip_starts_without_supervisor_or_members()
    {
        var group = NewTripGroup();

        Assert.False(group.HasSupervisor);
        Assert.Empty(group.Members);
        Assert.NotNull(group.TripId);
        Assert.Equal("Group.TripRequired",
            Group.CreateForTrip("G", "C", Guid.NewGuid(), Guid.Empty, Now).Error!.Code);
    }

    [Fact]
    public void No_one_can_announce_before_a_supervisor_is_assigned()
    {
        var group = NewTripGroup();

        Assert.Equal("Group.NotLeader", group.PostAnnouncement(Guid.NewGuid(), new AnnouncementContent("T", "B"), Now).Error!.Code);
    }

    [Fact]
    public void AssignSupervisor_promotes_a_member_and_replaces_the_previous_supervisor()
    {
        var group = NewTripGroup();
        var first = Guid.NewGuid();
        var pilgrim = Guid.NewGuid();
        group.AddMember(pilgrim, Now);

        Assert.True(group.AssignSupervisor(first, Now).IsSuccess);
        Assert.Equal(first, group.LeaderId);
        Assert.Equal(1, group.PilgrimCount);

        Assert.True(group.AssignSupervisor(pilgrim, Now).IsSuccess); // promote the existing member

        Assert.Equal(pilgrim, group.LeaderId);
        Assert.False(group.IsMember(first)); // previous supervisor left
        Assert.Equal(GroupMemberRole.Leader, Assert.Single(group.Members).Role);
        Assert.Equal(0, group.PilgrimCount);
    }

    [Fact]
    public void AssignSupervisor_again_is_idempotent()
    {
        var group = NewTripGroup();
        var supervisor = Guid.NewGuid();
        group.AssignSupervisor(supervisor, Now);
        group.ClearDomainEvents();

        Assert.True(group.AssignSupervisor(supervisor, Now).IsSuccess);
        Assert.Empty(group.DomainEvents);
        Assert.Single(group.Members);
    }

    [Fact]
    public void RemoveMember_rules()
    {
        var group = NewTripGroup();
        var supervisor = Guid.NewGuid();
        var pilgrim = Guid.NewGuid();
        group.AssignSupervisor(supervisor, Now);
        group.AddMember(pilgrim, Now);

        Assert.Equal("Group.CannotRemoveSupervisor", group.RemoveMember(supervisor).Error!.Code);
        Assert.True(group.RemoveMember(pilgrim).IsSuccess);
        Assert.Equal("Group.MemberNotFound", group.RemoveMember(pilgrim).Error!.Code);
    }

    [Fact]
    public void First_pilgrim_in_an_unsupervised_company_group_raises_GroupAwaitingSupervisor_once()
    {
        var group = NewTripGroup();
        group.ClearDomainEvents();

        group.AddMember(Guid.NewGuid(), Now);
        group.AddMember(Guid.NewGuid(), Now);

        Assert.Single(group.DomainEvents.OfType<GroupAwaitingSupervisor>());
    }

    [Fact]
    public void No_GroupAwaitingSupervisor_when_supervised_or_standalone()
    {
        var supervised = NewTripGroup();
        supervised.AssignSupervisor(Guid.NewGuid(), Now);
        supervised.AddMember(Guid.NewGuid(), Now);

        var standalone = Group.Create("G", "CODE", Guid.NewGuid(), Now).Value;
        standalone.AddMember(Guid.NewGuid(), Now);

        Assert.Empty(supervised.DomainEvents.OfType<GroupAwaitingSupervisor>());
        Assert.Empty(standalone.DomainEvents.OfType<GroupAwaitingSupervisor>());
    }

    [Fact]
    public void Only_the_supervisor_sets_a_valid_gathering_point()
    {
        var group = NewTripGroup();
        var supervisor = Guid.NewGuid();
        var pilgrim = Guid.NewGuid();
        group.AssignSupervisor(supervisor, Now);
        group.AddMember(pilgrim, Now);
        group.ClearDomainEvents();

        Assert.Equal("Group.NotLeader", group.SetGatheringPoint(pilgrim, "Gate 79", 21.42, 39.82, null, Now).Error!.Code);
        Assert.Equal("Group.AddressRequired", group.SetGatheringPoint(supervisor, " ", 21.42, 39.82, null, Now).Error!.Code);
        Assert.Equal("Group.InvalidCoordinates", group.SetGatheringPoint(supervisor, "Gate 79", 91, 39.82, null, Now).Error!.Code);
        Assert.Null(group.GatheringPoint);

        Assert.True(group.SetGatheringPoint(supervisor, " Gate 79 ", 21.42, 39.82, Now.AddHours(2), Now).IsSuccess);
        Assert.Equal("Gate 79", group.GatheringPoint!.Address);
        Assert.IsType<GatheringPointChanged>(Assert.Single(group.DomainEvents));
    }

    [Fact]
    public void Pilgrim_confirms_arrival_once_and_the_supervisor_can_override_and_reset()
    {
        var group = NewTripGroup();
        var supervisor = Guid.NewGuid();
        var pilgrim = Guid.NewGuid();
        group.AssignSupervisor(supervisor, Now);
        group.AddMember(pilgrim, Now);
        group.ClearDomainEvents();
        GroupMember Pilgrim() => group.Members.Single(m => m.UserId == pilgrim);

        Assert.Equal(AttendanceStatus.Absent, Pilgrim().AttendanceStatus);
        Assert.Equal("Group.NotPilgrim", group.ConfirmArrival(supervisor, Now).Error!.Code);

        Assert.True(group.ConfirmArrival(pilgrim, Now).IsSuccess);
        Assert.True(group.ConfirmArrival(pilgrim, Now.AddMinutes(1)).IsSuccess); // idempotent
        Assert.Single(group.DomainEvents.OfType<PilgrimArrived>());
        Assert.Equal(Now, Pilgrim().AttendanceConfirmedAtUtc);

        Assert.Equal("Group.NotLeader", group.SetAttendance(pilgrim, pilgrim, AttendanceStatus.Absent, Now).Error!.Code);
        Assert.True(group.SetAttendance(supervisor, pilgrim, AttendanceStatus.Absent, Now).IsSuccess);
        Assert.Null(Pilgrim().AttendanceConfirmedAtUtc);

        group.ConfirmArrival(pilgrim, Now);
        Assert.True(group.ResetAttendance(supervisor).IsSuccess);
        Assert.Equal(AttendanceStatus.Absent, Pilgrim().AttendanceStatus);
    }

    [Fact]
    public void Pilgrims_are_numbered_in_join_order_and_numbers_are_never_reused()
    {
        var group = NewTripGroup();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        group.AddMember(a, Now);
        group.AddMember(b, Now);
        group.RemoveMember(b);
        group.AddMember(c, Now);

        Assert.Equal(1, group.Members.Single(m => m.UserId == a).MemberNumber);
        Assert.Equal(3, group.Members.Single(m => m.UserId == c).MemberNumber);
    }

    [Fact]
    public void Only_the_supervisor_writes_member_notes()
    {
        var group = NewTripGroup();
        var supervisor = Guid.NewGuid();
        var pilgrim = Guid.NewGuid();
        group.AssignSupervisor(supervisor, Now);
        group.AddMember(pilgrim, Now);

        Assert.Equal("Group.NotLeader", group.SetMemberNotes(pilgrim, pilgrim, "x").Error!.Code);
        Assert.True(group.SetMemberNotes(supervisor, pilgrim, " Needs a wheelchair ").IsSuccess);
        Assert.Equal("Needs a wheelchair", group.Members.Single(m => m.UserId == pilgrim).Notes);
    }
}
