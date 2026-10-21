using Labaik.Domain.Common;
using Labaik.Domain.Groups.Enums;

namespace Labaik.Domain.Groups;

public sealed class GroupMember : Entity
{
    public const int NotesMaxLength = 2000;

    public Guid UserId { get; private set; }
    public GroupMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    // Pilgrims are numbered 1, 2, 3... in join order; numbers are never reused. Null for the supervisor
    // unless they were a numbered pilgrim before being promoted.
    public int? MemberNumber { get; private set; }

    // The supervisor's private notes about this member.
    public string? Notes { get; private set; }

    // Attendance at the current gathering (pilgrims only); Absent until confirmed.
    public AttendanceStatus AttendanceStatus { get; private set; } = AttendanceStatus.Absent;
    public DateTimeOffset? AttendanceConfirmedAtUtc { get; private set; }

    private GroupMember() { } // EF

    internal GroupMember(Guid userId, GroupMemberRole role, DateTimeOffset joinedAtUtc, int? memberNumber = null)
    {
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
        MemberNumber = memberNumber;
    }

    internal void PromoteToLeader() => Role = GroupMemberRole.Leader;

    // Group chat messages sent after this are unread for this member (null = nothing read yet).
    public DateTimeOffset? ChatLastReadAtUtc { get; private set; }
    public DateTimeOffset? ChatMutedUntilUtc { get; private set; }

    internal void SetNotes(string? notes) => Notes = notes;
    internal void MuteChat(DateTimeOffset? untilUtc) => ChatMutedUntilUtc = untilUtc;

    internal void MarkChatRead(DateTimeOffset readUpToUtc)
    {
        if (ChatLastReadAtUtc is null || readUpToUtc > ChatLastReadAtUtc)
        {
            ChatLastReadAtUtc = readUpToUtc;
        }
    }

    internal void SetAttendance(AttendanceStatus status, DateTimeOffset nowUtc)
    {
        AttendanceStatus = status;
        AttendanceConfirmedAtUtc = status == AttendanceStatus.Present ? nowUtc : null;
    }
}
