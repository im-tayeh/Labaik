using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace Labaik.Api.Realtime;

internal sealed class SignalRLocationBroadcaster(
    IHubContext<LocationHub> hubContext,
    ILogger<SignalRLocationBroadcaster> logger) : ILocationBroadcaster
{
    public Task LocationUpdatedAsync(IReadOnlyList<Guid> groupIds, LiveLocationDto location, CancellationToken cancellationToken = default)
        => SendAsync(groupIds, "LocationUpdated", location, cancellationToken);

    public Task StoppedSharingAsync(IReadOnlyList<Guid> groupIds, Guid userId, CancellationToken cancellationToken = default)
        => SendAsync(groupIds, "StoppedSharing", new { userId }, cancellationToken);

    // Best effort: the update is already saved, so a push failure is logged, never thrown.
    private async Task SendAsync(IReadOnlyList<Guid> groupIds, string method, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .Groups(groupIds.Select(GroupSubscribedHub.GroupName).ToList())
                .SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push {Method} to {GroupCount} group(s) failed.", method, groupIds.Count);
        }
    }
}
