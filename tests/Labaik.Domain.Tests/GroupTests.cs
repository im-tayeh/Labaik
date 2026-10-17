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

public class CompanyGroupDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static Group NewTripGroup() =>
        Group.CreateForTrip("Group A", "CODE01", Guid.NewGuid(), Guid.NewGuid(), Now).Value;

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

        Assert.Equal("Group.NotLeader", group.PostAnnouncement(Guid.NewGuid(), "T", "B", Now).Error!.Code);
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
}
