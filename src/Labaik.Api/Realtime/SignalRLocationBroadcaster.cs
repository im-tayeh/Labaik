using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

internal sealed class SignalRLocationBroadcaster(
    IHubContext<LocationHub> hubContext,
    ILogger<SignalRLocationBroadcaster> logger) : ILocationBroadcaster
{
    public Task LocationUpdatedAsync(IReadOnlyList<Guid> groupIds, LiveLocationDto location, CancellationToken cancellationToken = default)
        => SendAsync(groupIds.Select(GroupSubscribedHub.GroupName).ToList(), "LocationUpdated", location, cancellationToken);

    public Task StoppedSharingAsync(IReadOnlyList<Guid> groupIds, Guid userId, CancellationToken cancellationToken = default)
        => SendAsync(groupIds.Select(GroupSubscribedHub.GroupName).ToList(), "StoppedSharing", new { userId }, cancellationToken);

    public Task CircleLocationUpdatedAsync(IReadOnlyList<Guid> circleIds, LiveLocationDto location, CancellationToken cancellationToken = default)
        => SendAsync(circleIds.Select(LocationHub.CircleName).ToList(), "LocationUpdated", location, cancellationToken);

    public Task CircleStoppedSharingAsync(IReadOnlyList<Guid> circleIds, Guid userId, CancellationToken cancellationToken = default)
        => SendAsync(circleIds.Select(LocationHub.CircleName).ToList(), "StoppedSharing", new { userId }, cancellationToken);

    // Best effort: the update is already saved, so a push failure is logged, never thrown.
    private async Task SendAsync(IReadOnlyList<string> hubGroups, string method, object payload, CancellationToken cancellationToken)
    {
        if (hubGroups.Count == 0)
        {
            return;
        }

        try
        {
            await hubContext.Clients.Groups(hubGroups).SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push {Method} to {GroupCount} group(s) failed.", method, hubGroups.Count);
        }
    }
}
