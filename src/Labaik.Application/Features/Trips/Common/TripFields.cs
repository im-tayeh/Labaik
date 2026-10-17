using FluentValidation;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips;

namespace Labaik.Application.Features.Trips.Common;

// The editable trip fields shared by create and update (same JSON shape for both).
public interface ITripFields
{
    string Name { get; }
    PilgrimageType PilgrimageType { get; }
    List<string> Destinations { get; }
    DateOnly DepartureDate { get; }
    DateOnly? ReturnDate { get; }
    string DeparturePlace { get; }
    int ExpectedPilgrims { get; }
    string? Notes { get; }
}

public abstract class TripFieldsValidator<T> : AbstractValidator<T> where T : ITripFields
{
    protected TripFieldsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(TripErrors.NameMaxLength);
        RuleFor(x => x.PilgrimageType).IsInEnum();
        RuleFor(x => x.Destinations).NotEmpty().Must(d => d.Count <= TripErrors.MaxDestinations)
            .WithMessage($"At most {TripErrors.MaxDestinations} destinations.");
        RuleForEach(x => x.Destinations).NotEmpty().MaximumLength(TripErrors.DestinationMaxLength);
        RuleFor(x => x.DepartureDate).NotEmpty();
        RuleFor(x => x.ReturnDate).GreaterThanOrEqualTo(x => x.DepartureDate).When(x => x.ReturnDate is not null);
        RuleFor(x => x.DeparturePlace).NotEmpty().MaximumLength(TripErrors.PlaceMaxLength);
        RuleFor(x => x.ExpectedPilgrims).InclusiveBetween(1, TripErrors.MaxExpectedPilgrims);
        RuleFor(x => x.Notes).MaximumLength(TripErrors.NotesMaxLength);
    }
}

internal static class TripFieldsExtensions
{
    public static TripDetails ToDetails(this ITripFields f) => new(
        f.Name, f.PilgrimageType, f.Destinations, f.DepartureDate, f.ReturnDate, f.DeparturePlace, f.ExpectedPilgrims, f.Notes);
}
