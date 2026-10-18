using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles.Events;

namespace Labaik.Domain.FamilyCircles;

public enum FamilyCircleRole { Owner = 1, Member = 2 }

// A private circle of family/friends, independent of company groups. Anyone creates one and shares its
// join code; members see each other and, per circle, choose whether to show their live location there.
public sealed class FamilyCircle : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int MaxMembers = 20;

    public string Name { get; private set; } = string.Empty;
    public string JoinCode { get; private set; } = string.Empty;
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private readonly List<FamilyCircleMember> _members = [];
    public IReadOnlyList<FamilyCircleMember> Members => _members.AsReadOnly();

    private FamilyCircle() { } // EF

    public static Result<FamilyCircle> Create(string name, Guid ownerId, string joinCode, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name)) return FamilyCircleErrors.NameRequired;
        name = name.Trim();
        if (name.Length > NameMaxLength) return FamilyCircleErrors.NameTooLong;
        if (ownerId == Guid.Empty) return FamilyCircleErrors.UserRequired;
        if (string.IsNullOrWhiteSpace(joinCode)) return FamilyCircleErrors.JoinCodeRequired;

        var circle = new FamilyCircle { Name = name, OwnerId = ownerId, JoinCode = joinCode.Trim(), CreatedAtUtc = nowUtc };
        circle._members.Add(new FamilyCircleMember(ownerId, FamilyCircleRole.Owner, nowUtc));
        return circle;
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public Result Join(Guid userId, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) return Result.Failure(FamilyCircleErrors.UserRequired);
        if (IsMember(userId)) return Result.Failure(FamilyCircleErrors.AlreadyMember);
        if (_members.Count >= MaxMembers) return Result.Failure(FamilyCircleErrors.Full);

        _members.Add(new FamilyCircleMember(userId, FamilyCircleRole.Member, nowUtc));
        Raise(new FamilyMemberJoined(Id, userId, OwnerId));
        return Result.Success();
    }

    // The owner removes anyone else; any member may remove themself (leave). The owner cannot leave.
    public Result RemoveMember(Guid actorId, Guid userId)
    {
        if (!IsMember(actorId)) return Result.Failure(FamilyCircleErrors.NotFound);
        if (actorId != userId && actorId != OwnerId) return Result.Failure(FamilyCircleErrors.NotOwner);
        if (userId == OwnerId) return Result.Failure(FamilyCircleErrors.OwnerCannotLeave);

        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) return Result.Failure(FamilyCircleErrors.MemberNotFound);

        _members.Remove(member);
        if (member.IsSharingLocation)
        {
            Raise(new FamilyLocationSharingChanged(Id, userId, IsSharing: false));
        }

        return Result.Success();
    }

    // Show or hide my live location in this circle (my global location sharing must also be on).
    public Result SetLocationSharing(Guid userId, bool isSharing)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) return Result.Failure(FamilyCircleErrors.NotFound);

        if (member.IsSharingLocation != isSharing)
        {
            member.SetLocationSharing(isSharing);
            Raise(new FamilyLocationSharingChanged(Id, userId, isSharing));
        }

        return Result.Success();
    }
}

public sealed class FamilyCircleMember : Entity
{
    public Guid UserId { get; private set; }
    public FamilyCircleRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public bool IsSharingLocation { get; private set; }

    private FamilyCircleMember() { } // EF

    internal FamilyCircleMember(Guid userId, FamilyCircleRole role, DateTimeOffset joinedAtUtc)
    {
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    internal void SetLocationSharing(bool isSharing) => IsSharingLocation = isSharing;
}
