using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Rituals;

public static class RitualErrors
{
    public static Error UserRequired =>
        Error.Validation("Ritual.UserRequired", "Ritual progress must belong to a user.");

    public static Error InvalidOrder =>
        Error.Validation("Ritual.InvalidOrder", "Step order must be greater than zero.");

    public static Error InvalidRound =>
        Error.Validation("Ritual.InvalidRound", "Tawaf rounds and Saee laps must be between 1 and 7.");

    public static Error TitleRequired =>
        Error.Validation("Ritual.TitleRequired", "Step title is required.");

    public static Error BodyRequired =>
        Error.Validation("Ritual.BodyRequired", "Step body is required.");

    public static Error NoStepsAvailable =>
        Error.NotFound("Ritual.NoSteps", "No published ritual steps are available for this pilgrimage type.");

    public static Error StepNotFound =>
        Error.NotFound("Ritual.StepNotFound", "The ritual step was not found.");

    public static Error AlreadyCompleted =>
        Error.Conflict("Ritual.AlreadyCompleted", "All ritual steps are already completed.");

    public static Error StepOutOfOrder(int expected, int actual) =>
        Error.Conflict("Ritual.StepOutOfOrder", $"Expected to complete step {expected}, but step {actual} was submitted.");
}
