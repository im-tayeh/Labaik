using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Common;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Domain.Locations.Events;
using MediatR;

namespace Labaik.Application.Features.Locations.EventHandlers;

public sealed class MemberLocationUpdatedHandler(
    IAppDbContext context,
    ILocationBroadcaster broadcaster) : INotificationHandler<DomainEventNotification<MemberLocationUpdated>>
{
    public async Task Handle(DomainEventNotification<MemberLocationUpdated> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var groupIds = await context.GetGroupIdsOfUserAsync(e.UserId, cancellationToken);
        if (groupIds.Count == 0)
        {
            return;
        }

        await broadcaster.LocationUpdatedAsync(
            groupIds, new LiveLocationDto(e.UserId, e.Latitude, e.Longitude, e.AccuracyMeters, e.UpdatedAtUtc), cancellationToken);
    }
}

public sealed class MemberStoppedSharingLocationHandler(
    IAppDbContext context,
    ILocationBroadcaster broadcaster) : INotificationHandler<DomainEventNotification<MemberStoppedSharingLocation>>
{
    public async Task Handle(DomainEventNotification<MemberStoppedSharingLocation> notification, CancellationToken cancellationToken)
    {
        var userId = notification.DomainEvent.UserId;
        var groupIds = await context.GetGroupIdsOfUserAsync(userId, cancellationToken);
        if (groupIds.Count == 0)
        {
            return;
        }

        await broadcaster.StoppedSharingAsync(groupIds, userId, cancellationToken);
    }
}
