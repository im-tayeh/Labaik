using Labaik.Domain.Common;
using Labaik.Domain.Groups.Enums;

namespace Labaik.Domain.Groups;

public sealed class GroupMember : Entity
{
    public Guid UserId { get; private set; }
    public GroupMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    // Attendance at the current gathering (pilgrims only); Absent until confirmed.
    public AttendanceStatus AttendanceStatus { get; private set; } = AttendanceStatus.Absent;
    public DateTimeOffset? AttendanceConfirmedAtUtc { get; private set; }

    private GroupMember() { } // EF

    internal GroupMember(Guid userId, GroupMemberRole role, DateTimeOffset joinedAtUtc)
    {
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    internal void PromoteToLeader() => Role = GroupMemberRole.Leader;

    internal void SetAttendance(AttendanceStatus status, DateTimeOffset nowUtc)
    {
        AttendanceStatus = status;
        AttendanceConfirmedAtUtc = status == AttendanceStatus.Present ? nowUtc : null;
    }
}
