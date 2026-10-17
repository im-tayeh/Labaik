using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Trips.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Trips.Itinerary;

public sealed record ItineraryActivityDto(string Title, DateTimeOffset? TimeUtc, string? Place);

public sealed record ItineraryDayDto(DateOnly Date, string? Title, IReadOnlyList<ItineraryActivityDto> Activities);

// Days ordered by date; UpdatedAtUtc is null until the program is first set.
public sealed record TripItineraryDto(Guid TripId, string TripName, IReadOnlyList<ItineraryDayDto> Days, DateTimeOffset? UpdatedAtUtc);

internal static class ItineraryMapper
{
    public static TripItineraryDto ToItineraryDto(this Trip t) => new(
        t.Id, t.Name,
        t.Itinerary.Select(d => new ItineraryDayDto(
            d.Date, d.Title, d.Activities.Select(a => new ItineraryActivityDto(a.Title, a.TimeUtc, a.Place)).ToList())).ToList(),
        t.ItineraryUpdatedAtUtc);
}

// --- Read: the owning company, and supervisors and pilgrims of the trip's groups (others -> 404) ---

public sealed record GetTripItineraryQuery(Guid TripId) : IRequest<Result<TripItineraryDto>>;

public sealed class GetTripItineraryQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetTripItineraryQuery, Result<TripItineraryDto>>
{
    public async Task<Result<TripItineraryDto>> Handle(GetTripItineraryQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var canSee = await context.IsTripMemberAsync(request.TripId, userId, cancellationToken)
                     || await context.IsTripCompanyOwnerAsync(request.TripId, userId, cancellationToken);
        if (!canSee)
        {
            return TripErrors.NotFound;
        }

        var trip = await context.Trips.AsNoTracking().FirstAsync(t => t.Id == request.TripId, cancellationToken);
        return trip.ToItineraryDto();
    }
}

// --- Replace: the owning company or a supervisor of the trip (pilgrims -> 403, others -> 404) ---

public sealed record ReplaceTripItineraryRequest(List<ItineraryDayDto> Days);

public sealed record ReplaceTripItineraryCommand(Guid TripId, List<ItineraryDayDto> Days) : IRequest<Result<TripItineraryDto>>;

public sealed class ReplaceTripItineraryCommandValidator : AbstractValidator<ReplaceTripItineraryCommand>
{
    public ReplaceTripItineraryCommandValidator()
    {
        RuleFor(x => x.Days).NotNull().Must(d => d.Count <= TripErrors.MaxItineraryDays)
            .WithMessage($"At most {TripErrors.MaxItineraryDays} days.");
        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.Title).MaximumLength(TripErrors.ActivityTextMaxLength);
            day.RuleFor(d => d.Activities).NotNull().Must(a => a.Count <= TripErrors.MaxActivitiesPerDay)
                .WithMessage($"At most {TripErrors.MaxActivitiesPerDay} activities per day.");
            day.RuleForEach(d => d.Activities).ChildRules(a =>
            {
                a.RuleFor(x => x.Title).NotEmpty().MaximumLength(TripErrors.ActivityTextMaxLength);
                a.RuleFor(x => x.Place).MaximumLength(TripErrors.ActivityTextMaxLength);
            });
        });
    }
}

public sealed class ReplaceTripItineraryCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<ReplaceTripItineraryCommand, Result<TripItineraryDto>>
{
    public async Task<Result<TripItineraryDto>> Handle(ReplaceTripItineraryCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var canEdit = await context.IsTripCompanyOwnerAsync(request.TripId, userId, cancellationToken)
                      || await context.IsTripSupervisorAsync(request.TripId, userId, cancellationToken);
        if (!canEdit)
        {
            return await context.IsTripMemberAsync(request.TripId, userId, cancellationToken)
                ? TripErrors.CannotEditItinerary
                : TripErrors.NotFound;
        }

        var trip = await context.Trips.FirstAsync(t => t.Id == request.TripId, cancellationToken);

        var days = request.Days
            .Select(d => new ItineraryDayInput(d.Date, d.Title,
                (d.Activities ?? []).Select(a => new ItineraryActivityInput(a.Title, a.TimeUtc, a.Place)).ToList()))
            .ToList();

        var replace = trip.ReplaceItinerary(days, timeProvider.GetUtcNow());
        if (replace.IsFailure)
        {
            return replace.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return trip.ToItineraryDto();
    }
}
