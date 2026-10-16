using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Locations.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Locations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Locations.Commands.UpdateMyLocation;

// Sent over REST or the SignalR hub; group members are notified via the MemberLocationUpdated event.
public sealed record UpdateMyLocationCommand(double Latitude, double Longitude, double? AccuracyMeters) : IRequest<Result>;

public sealed class UpdateMyLocationCommandValidator : AbstractValidator<UpdateMyLocationCommand>
{
    public UpdateMyLocationCommandValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0).When(x => x.AccuracyMeters is not null);
    }
}

public sealed class UpdateMyLocationCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<UpdateMyLocationCommand, Result>
{
    public async Task<Result> Handle(UpdateMyLocationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        // Never auto-create here: a position is only accepted after the pilgrim opted in.
        var share = await context.LocationShares.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (share is null)
        {
            return Result.Failure(LocationErrors.SharingDisabled);
        }

        var update = share.UpdatePosition(request.Latitude, request.Longitude, request.AccuracyMeters, timeProvider.GetUtcNow());
        if (update.IsFailure)
        {
            return update;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
