using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Common;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Locations.Commands.SetLocationSharing;

// Opt in/out. Turning sharing off erases the last known position.
public sealed record SetLocationSharingCommand(bool IsSharing) : IRequest<Result<MyLocationSharingDto>>;

public sealed class SetLocationSharingCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SetLocationSharingCommand, Result<MyLocationSharingDto>>
{
    public async Task<Result<MyLocationSharingDto>> Handle(SetLocationSharingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var load = await context.GetOrCreateShareAsync(userId, cancellationToken);
        if (load.IsFailure)
        {
            return load.Error!;
        }

        var share = load.Value;
        if (request.IsSharing)
        {
            share.StartSharing();
        }
        else
        {
            share.StopSharing();
        }

        await context.SaveChangesAsync(cancellationToken);

        return share.ToMyDto(timeProvider.GetUtcNow());
    }
}
