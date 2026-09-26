using Labaik.Domain.Common;
using Labaik.Domain.Groups.Enums;

namespace Labaik.Domain.Groups;

public sealed class GroupMember : Entity
{
    public Guid UserId { get; private set; }
    public GroupMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    private GroupMember() { } // EF

    internal GroupMember(Guid userId, GroupMemberRole role, DateTimeOffset joinedAtUtc)
    {
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }
}