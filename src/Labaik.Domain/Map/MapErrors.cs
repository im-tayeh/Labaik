using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Map;

public static class MapErrors
{
    public const int NoteMaxLength = 500;

    public static Error InvalidKey =>
        Error.Validation("Map.InvalidKey", $"A map place needs a key of at most {MapPlace.KeyMaxLength} characters.");

    public static Error InvalidContent =>
        Error.Validation("Map.InvalidContent", "A map place needs a name; texts must fit their limits.");

    public static Error InvalidCoordinates =>
        Error.Validation("Map.InvalidCoordinates", "Latitude must be within -90..90 and longitude within -180..180.");

    public static Error FacilityKindMismatch =>
        Error.Validation("Map.FacilityKindMismatch", "Facilities need a facility kind; gates must not have one.");

    public static Error PlaceNotFound =>
        Error.NotFound("Map.PlaceNotFound", "Map place not found.");

    public static Error UserRequired =>
        Error.Validation("Map.UserRequired", "A user is required.");

    public static Error NoteTooLong =>
        Error.Validation("Map.NoteTooLong", $"The note must be at most {NoteMaxLength} characters.");

    public static Error EmergencyNotFound =>
        Error.NotFound("Map.EmergencyNotFound", "Emergency alert not found.");
}
