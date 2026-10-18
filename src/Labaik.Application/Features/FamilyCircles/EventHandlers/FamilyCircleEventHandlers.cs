using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Application.Features.Notifications.EventHandlers;
using Labaik.Domain.FamilyCircles.Events;
using Labaik.Domain.Locations.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.FamilyCircles.EventHandlers;

// A position update (REST or hub) also reaches the circles where the user shows their location.
public sealed class FamilyLocationUpdatedHandler(
    IAppDbContext context,
    ILocationBroadcaster broadcaster) : INotificationHandler<DomainEventNotification<MemberLocationUpdated>>
{
    public async Task Handle(DomainEventNotification<MemberLocationUpdated> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var circleIds = await context.GetCirclesSharingLocationOfAsync(e.UserId, cancellationToken);
        await broadcaster.CircleLocationUpdatedAsync(
            circleIds, new LiveLocationDto(e.UserId, e.Latitude, e.Longitude, e.AccuracyMeters, e.UpdatedAtUtc), cancellationToken);
    }
}

// Global sharing turned off -> hide the marker in every circle too.
public sealed class FamilyStoppedSharingHandler(
    IAppDbContext context,
    ILocationBroadcaster broadcaster) : INotificationHandler<DomainEventNotification<MemberStoppedSharingLocation>>
{
    public async Task Handle(DomainEventNotification<MemberStoppedSharingLocation> notification, CancellationToken cancellationToken)
    {
        var userId = notification.DomainEvent.UserId;
        var circleIds = await context.GetCirclesSharingLocationOfAsync(userId, cancellationToken);
        await broadcaster.CircleStoppedSharingAsync(circleIds, userId, cancellationToken);
    }
}

// Per-circle toggle: on -> push the last fresh position right away; off -> hide the marker.
public sealed class FamilyLocationSharingChangedHandler(
    IAppDbContext context,
    ILocationBroadcaster broadcaster,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<FamilyLocationSharingChanged>>
{
    public async Task Handle(DomainEventNotification<FamilyLocationSharingChanged> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        if (!e.IsSharing)
        {
            await broadcaster.CircleStoppedSharingAsync([e.CircleId], e.UserId, cancellationToken);
            return;
        }

        var share = await context.LocationShares.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == e.UserId, cancellationToken);
        if (share is { IsSharing: true, HasPosition: true } && !share.IsStale(timeProvider.GetUtcNow()))
        {
            await broadcaster.CircleLocationUpdatedAsync(
                [e.CircleId],
                new LiveLocationDto(e.UserId, share.Latitude!.Value, share.Longitude!.Value, share.AccuracyMeters, share.UpdatedAtUtc!.Value),
                cancellationToken);
        }
    }
}

// Someone joined my circle -> notify the owner (type 12, referenceId = circle id, preview = their name).
public sealed class FamilyMemberJoinedHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<FamilyMemberJoined>>
{
    public async Task Handle(DomainEventNotification<FamilyMemberJoined> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var name = await userDirectory.GetFullNameAsync(e.UserId, cancellationToken);
        await NotificationWriter.CreateAndPushAsync(
            context, notifier, [e.OwnerId], NotificationType.FamilyMemberJoined,
            groupId: null, e.CircleId, name, timeProvider.GetUtcNow(), cancellationToken);
    }
}
