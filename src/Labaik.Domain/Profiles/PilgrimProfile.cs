using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Profiles.Enums;

namespace Labaik.Domain.Profiles;

// Personal details a pilgrim chooses to share with their supervisors. One per user; edited only by the user.
// HealthNotes is sensitive personal data: shown only to the user and to supervisors of groups they belong to.
public sealed class PilgrimProfile : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Gender? Gender { get; private set; }
    public string? Nationality { get; private set; } // ISO 3166-1 alpha-2, upper case
    public string? PhotoUrl { get; private set; }
    public string? HealthNotes { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private PilgrimProfile() { } // EF

    private PilgrimProfile(Guid userId) => UserId = userId;

    public static Result<PilgrimProfile> Create(Guid userId)
        => userId == Guid.Empty ? ProfileErrors.UserRequired : new PilgrimProfile(userId);

    // Replaces all fields (null clears one). Validates everything first, so a failure changes nothing.
    public Result Update(Gender? gender, string? nationality, string? photoUrl, string? healthNotes, DateTimeOffset nowUtc)
    {
        if (gender is { } g && !Enum.IsDefined(g))
        {
            return Result.Failure(ProfileErrors.InvalidGender);
        }

        nationality = string.IsNullOrWhiteSpace(nationality) ? null : nationality.Trim().ToUpperInvariant();
        if (nationality is not null && (nationality.Length != 2 || !nationality.All(char.IsAsciiLetterUpper)))
        {
            return Result.Failure(ProfileErrors.InvalidNationality);
        }

        photoUrl = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();
        if (photoUrl is not null && (photoUrl.Length > ProfileErrors.PhotoUrlMaxLength
            || !Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure(ProfileErrors.InvalidPhotoUrl);
        }

        healthNotes = string.IsNullOrWhiteSpace(healthNotes) ? null : healthNotes.Trim();
        if (healthNotes is { Length: > ProfileErrors.HealthNotesMaxLength })
        {
            return Result.Failure(ProfileErrors.HealthNotesTooLong);
        }

        Gender = gender;
        Nationality = nationality;
        PhotoUrl = photoUrl;
        HealthNotes = healthNotes;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }
}
