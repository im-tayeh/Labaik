using Labaik.Domain.FamilyCircles;
using Labaik.Domain.FamilyCircles.Events;

namespace Labaik.Domain.Tests;

public class FamilyCircleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    private static FamilyCircle NewCircle() => FamilyCircle.Create(" Al Saleh family ", Owner, "ABC234", Now).Value;

    [Fact]
    public void Create_adds_the_owner_and_validates()
    {
        var circle = NewCircle();

        Assert.Equal("Al Saleh family", circle.Name);
        Assert.Equal(FamilyCircleRole.Owner, Assert.Single(circle.Members).Role);
        Assert.Equal("FamilyCircle.NameRequired", FamilyCircle.Create(" ", Owner, "X", Now).Error!.Code);
    }

    [Fact]
    public void Join_rejects_duplicates_and_a_full_circle()
    {
        var circle = NewCircle();
        var member = Guid.NewGuid();

        Assert.True(circle.Join(member, Now).IsSuccess);
        Assert.IsType<FamilyMemberJoined>(Assert.Single(circle.DomainEvents));
        Assert.Equal("FamilyCircle.AlreadyMember", circle.Join(member, Now).Error!.Code);

        while (circle.Members.Count < FamilyCircle.MaxMembers)
        {
            circle.Join(Guid.NewGuid(), Now);
        }
        Assert.Equal("FamilyCircle.Full", circle.Join(Guid.NewGuid(), Now).Error!.Code);
    }

    [Fact]
    public void Owner_removes_others_members_leave_owner_stays()
    {
        var circle = NewCircle();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        circle.Join(a, Now);
        circle.Join(b, Now);

        Assert.Equal("FamilyCircle.NotOwner", circle.RemoveMember(a, b).Error!.Code);
        Assert.True(circle.RemoveMember(a, a).IsSuccess);          // leave
        Assert.True(circle.RemoveMember(Owner, b).IsSuccess);      // owner removes
        Assert.Equal("FamilyCircle.OwnerCannotLeave", circle.RemoveMember(Owner, Owner).Error!.Code);
        Assert.Single(circle.Members);
    }

    [Fact]
    public void Location_sharing_toggle_raises_an_event_only_on_change()
    {
        var circle = NewCircle();
        circle.ClearDomainEvents();

        Assert.True(circle.SetLocationSharing(Owner, true).IsSuccess);
        Assert.True(circle.SetLocationSharing(Owner, true).IsSuccess);
        Assert.True(Assert.IsType<FamilyLocationSharingChanged>(Assert.Single(circle.DomainEvents)).IsSharing);
        Assert.Equal("FamilyCircle.NotFound", circle.SetLocationSharing(Guid.NewGuid(), true).Error!.Code);
    }
}
