using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Journeys.Events;
using Labaik.Domain.Journeys.ValueObjects;

namespace Labaik.Domain.Journeys;

public sealed class Journey : AggregateRoot
{
    public Guid UserId { get; private set; }
    public PilgrimageType PilgrimageType { get; private set; }
    public bool IsFirstTime { get; private set; }
    public TravelParty TravelParty { get; private set; }
    public JourneyStatus Status { get; private set; }
    public ArrivalPlan? ArrivalPlan { get; private set; }

    private readonly List<Stay> _stays = [];
    public IReadOnlyList<Stay> Stays => _stays.AsReadOnly();

    private bool IsEditable => Status is JourneyStatus.Draft;

    private Journey() { } // EF

    private Journey(Guid userId, PilgrimageType type, bool isFirstTime, TravelParty party)
    {
        UserId = userId;
        PilgrimageType = type;
        IsFirstTime = isFirstTime;
        TravelParty = party;
        Status = JourneyStatus.Draft;

        Raise(new JourneyCreated(Id, userId));
    }

    // Factory: the only way to create a Journey (starts as Draft).
    public static Result<Journey> Create(
        Guid userId, PilgrimageType type, bool isFirstTime, TravelParty party)
    {
        if (userId == Guid.Empty)
        {
            return JourneyErrors.UserRequired;
        }

        return new Journey(userId, type, isFirstTime, party);
    }

    public Result SetArrivalPlan(TransportMode mode, string entryPoint)
    {
        if (!IsEditable)
        {
            return Result.Failure(JourneyErrors.NotEditable(Status));
        }

        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            return Result.Failure(JourneyErrors.EntryPointRequired);
        }

        ArrivalPlan = new ArrivalPlan(mode, entryPoint.Trim());
        return Result.Success();
    }

    public Result AddStay(string cityName, AccommodationType type, string placeName, DateTimeOffset arrivalAtUtc)
    {
        if (!IsEditable) return Result.Failure(JourneyErrors.NotEditable(Status));
        if (string.IsNullOrWhiteSpace(cityName)) return Result.Failure(JourneyErrors.CityRequired);
        if (string.IsNullOrWhiteSpace(placeName)) return Result.Failure(JourneyErrors.PlaceNameRequired);

        _stays.Add(new Stay(cityName.Trim(), type, placeName.Trim(), arrivalAtUtc));
        return Result.Success();
    }

    public Result ClearStays()
    {
        if (!IsEditable)
        {
            return Result.Failure(JourneyErrors.NotEditable(Status));
        }

        _stays.Clear();
        return Result.Success();
    }

    // Draft -> Ready: enforce the journey is complete enough to use.
    public Result MarkReady()
    {
        if (Status is not JourneyStatus.Draft)
        {
            return Result.Failure(JourneyErrors.InvalidStatusTransition(Status, JourneyStatus.Ready));
        }

        if (ArrivalPlan is null)
        {
            return Result.Failure(JourneyErrors.ArrivalPlanRequired);
        }

        if (_stays.Count == 0)
        {
            return Result.Failure(JourneyErrors.StayRequired);
        }

        Status = JourneyStatus.Ready;
        Raise(new JourneyReadied(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status is not JourneyStatus.Ready)
        {
            return Result.Failure(JourneyErrors.InvalidStatusTransition(Status, JourneyStatus.Active));
        }

        Status = JourneyStatus.Active;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status is not JourneyStatus.Active)
        {
            return Result.Failure(JourneyErrors.InvalidStatusTransition(Status, JourneyStatus.Completed));
        }

        Status = JourneyStatus.Completed;
        return Result.Success();
    }
}