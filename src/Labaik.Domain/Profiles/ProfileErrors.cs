using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Profiles;

public static class ProfileErrors
{
    public const int HealthNotesMaxLength = 2000;
    public const int PhotoUrlMaxLength = 500;

    public static Error UserRequired =>
        Error.Validation("Profile.UserRequired", "A profile must belong to a user.");

    public static Error InvalidNationality =>
        Error.Validation("Profile.InvalidNationality", "Nationality must be a 2-letter ISO 3166-1 country code.");

    public static Error InvalidPhotoUrl =>
        Error.Validation("Profile.InvalidPhotoUrl", "Photo URL must be an absolute https URL.");

    public static Error HealthNotesTooLong =>
        Error.Validation("Profile.HealthNotesTooLong", $"Health notes must be at most {HealthNotesMaxLength} characters.");

    public static Error InvalidGender =>
        Error.Validation("Profile.InvalidGender", "Gender must be 1 (male) or 2 (female).");
}
