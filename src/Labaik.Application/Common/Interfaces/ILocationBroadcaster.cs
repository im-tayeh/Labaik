using Labaik.Application.Features.Locations.Dtos;

namespace Labaik.Application.Common.Interfaces;

// Pushes live location changes to connected members of the given groups / family circles (SignalR in the
// Api layer). Implementations must not throw: a failed push must never fail the saved update.
public interface ILocationBroadcaster
{
    Task LocationUpdatedAsync(IReadOnlyList<Guid> groupIds, LiveLocationDto location, CancellationToken cancellationToken = default);

    Task StoppedSharingAsync(IReadOnlyList<Guid> groupIds, Guid userId, CancellationToken cancellationToken = default);

    Task CircleLocationUpdatedAsync(IReadOnlyList<Guid> circleIds, LiveLocationDto location, CancellationToken cancellationToken = default);

    Task CircleStoppedSharingAsync(IReadOnlyList<Guid> circleIds, Guid userId, CancellationToken cancellationToken = default);
}
