using Labaik.Domain.Common.Results;

namespace Labaik.Domain.FamilyCircles;

public static class FamilyCircleErrors
{
    public static Error NameRequired =>
        Error.Validation("FamilyCircle.NameRequired", "Circle name is required.");

    public static Error NameTooLong =>
        Error.Validation("FamilyCircle.NameTooLong", $"Circle name must be at most {FamilyCircle.NameMaxLength} characters.");

    public static Error UserRequired =>
        Error.Validation("FamilyCircle.UserRequired", "A user is required.");

    public static Error JoinCodeRequired =>
        Error.Validation("FamilyCircle.JoinCodeRequired", "A join code is required.");

    public static Error AlreadyMember =>
        Error.Conflict("FamilyCircle.AlreadyMember", "You are already in this circle.");

    public static Error Full =>
        Error.Conflict("FamilyCircle.Full", $"A circle can have at most {FamilyCircle.MaxMembers} members.");

    public static Error NotOwner =>
        Error.Forbidden("FamilyCircle.NotOwner", "Only the circle's owner can remove other members.");

    public static Error OwnerCannotLeave =>
        Error.Conflict("FamilyCircle.OwnerCannotLeave", "The circle's owner cannot leave or be removed.");

    public static Error MemberNotFound =>
        Error.NotFound("FamilyCircle.MemberNotFound", "This person is not in the circle.");

    public static Error NotFound =>
        Error.NotFound("FamilyCircle.NotFound", "Circle not found.");
}
