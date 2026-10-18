using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;

namespace Labaik.Application.Features.FamilyCircles.Commands.SetFamilyLocationSharing;

// Show/hide my live location in this circle. Positions still come from the one location pipeline
// (PUT /location/sharing + POST /location or the hub), so global sharing must be on as well.
public sealed record SetFamilyLocationSharingRequest(bool IsSharing);

public sealed record SetFamilyLocationSharingCommand(Guid CircleId, bool IsSharing) : IRequest<Result<FamilyLocationSharingDto>>;

public sealed class SetFamilyLocationSharingCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<SetFamilyLocationSharingCommand, Result<FamilyLocationSharingDto>>
{
    public async Task<Result<FamilyLocationSharingDto>> Handle(SetFamilyLocationSharingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var circle = await context.GetCircleForMemberAsync(request.CircleId, userId, cancellationToken);
        if (circle is null)
        {
            return FamilyCircleErrors.NotFound;
        }

        var set = circle.SetLocationSharing(userId, request.IsSharing);
        if (set.IsFailure)
        {
            return set.Error!;
        }

        await context.SaveChangesAsync(cancellationToken); // FamilyLocationSharingChanged -> realtime
        return new FamilyLocationSharingDto(circle.Id, request.IsSharing);
    }
}
