using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Groups;

public static class GroupErrors
{
    public static Error NameRequired =>
        Error.Validation("Group.NameRequired", "Group name is required.");

    public static Error JoinCodeRequired =>
        Error.Validation("Group.JoinCodeRequired", "A join code is required.");

    public static Error LeaderRequired =>
        Error.Validation("Group.LeaderRequired", "A group must have a leader.");

    public static Error AlreadyMember =>
        Error.Conflict("Group.AlreadyMember", "The user is already a member of this group.");
}
