using Labaik.Application.Features.Rituals.Dtos;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Journeys.Enums;

namespace Labaik.Application.Features.Home.Dtos;

public sealed record HomeJourneyDto(Guid Id, PilgrimageType PilgrimageType, JourneyStatus Status, bool IsFirstTime);

// IsStarted=false means the pilgrim has not opened the guide yet; NextStep is then the first step.
public sealed record HomeRitualDto(
    PilgrimageType PilgrimageType,
    bool IsStarted,
    bool IsCompleted,
    int CompletedCount,
    int TotalSteps,
    RitualStepDto? NextStep);

public sealed record HomeGroupDto(
    Guid Id, string Name, string? LeaderName, int MemberCount, GroupMemberRole MyRole, int UnreadAnnouncements,
    Guid? TripId, // null for standalone groups; use it for GET /trips/{tripId}/itinerary
    int UnreadMessages); // group chat messages from others after my read marker

// Every section is null when it does not apply (no journey yet, no published steps, not in a group).
public sealed record HomeDto(HomeJourneyDto? Journey, HomeRitualDto? Ritual, HomeGroupDto? Group, int UnreadNotifications);
