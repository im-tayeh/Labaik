using Labaik.Domain.Journeys.Enums;

namespace Labaik.Domain.Journeys.ValueObjects;

// Value object: identity-less, immutable, compared by value (record does this for us).
public sealed record ArrivalPlan(TransportMode Mode, string EntryPoint);