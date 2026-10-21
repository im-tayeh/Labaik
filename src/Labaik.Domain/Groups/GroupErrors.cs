using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Groups;

public static class GroupErrors
{
    public const int NameMaxLength = 100;

    public static Error NameTooLong =>
        Error.Validation("Group.NameTooLong", $"Group name must be at most {NameMaxLength} characters.");

    public static Error TripRequired =>
        Error.Validation("Group.TripRequired", "A company group must belong to a company trip.");

    public static Error AddressRequired =>
        Error.Validation("Group.AddressRequired", "The gathering point address is required.");

    public static Error AddressTooLong =>
        Error.Validation("Group.AddressTooLong", "The gathering point address is too long.");

    public static Error InvalidCoordinates =>
        Error.Validation("Group.InvalidCoordinates", "Latitude must be between -90 and 90 and longitude between -180 and 180.");

    public static Error NotPilgrim =>
        Error.Forbidden("Group.NotPilgrim", "Only pilgrims (not the supervisor) confirm attendance.");

    public static Error InvalidAttendanceStatus =>
        Error.Validation("Group.InvalidAttendanceStatus", "Attendance status must be Present (1) or Absent (2).");

    public static Error NotesTooLong =>
        Error.Validation("Group.NotesTooLong", "Member notes must be at most 2000 characters.");

    public static Error MemberNotFound =>
        Error.NotFound("Group.MemberNotFound", "The user is not a member of this group.");

    public static Error CannotRemoveSupervisor =>
        Error.Conflict("Group.CannotRemoveSupervisor", "The supervisor cannot be removed as a member; assign another supervisor instead.");

    public static Error NameRequired =>
        Error.Validation("Group.NameRequired", "Group name is required.");

    public static Error JoinCodeRequired =>
        Error.Validation("Group.JoinCodeRequired", "A join code is required.");

    public static Error LeaderRequired =>
        Error.Validation("Group.LeaderRequired", "A group must have a leader.");

    public static Error NotFound =>
        Error.NotFound("Group.NotFound", "The group was not found.");

    public static Error NotLeader =>
        Error.Forbidden("Group.NotLeader", "Only the group leader can do this.");

    public static Error AlreadyMember =>
        Error.Conflict("Group.AlreadyMember", "The user is already a member of this group.");

    public static Error GroupNotesTooLong =>
        Error.Validation("Group.GroupNotesTooLong", $"Group notes must be at most {Group.NotesMaxLength} characters.");
    public static Error InvalidCapacity =>
        Error.Validation("Group.InvalidCapacity", "Capacity must be at least 1.");
    public static Error CapacityBelowMembers =>
        Error.Conflict("Group.CapacityBelowMembers", "The group already has more pilgrims than this capacity.");
    public static Error CapacityReached =>
        Error.Conflict("Group.CapacityReached", "The group is full.");
    public static Error HasMembers =>
        Error.Conflict("Group.HasMembers", "Remove the group's pilgrims before deleting it.");
}
