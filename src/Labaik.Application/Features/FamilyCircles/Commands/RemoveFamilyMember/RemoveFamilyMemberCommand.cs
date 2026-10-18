using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;

namespace Labaik.Application.Features.FamilyCircles.Commands.RemoveFamilyMember;

// The owner removes a member, or a member removes themself (leave). Owner cannot leave (409).
public sealed record RemoveFamilyMemberCommand(Guid CircleId, Guid UserId) : IRequest<Result>;

public sealed class RemoveFamilyMemberCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<RemoveFamilyMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveFamilyMemberCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        var circle = await context.GetCircleForMemberAsync(request.CircleId, userId, cancellationToken);
        if (circle is null)
        {
            return Result.Failure(FamilyCircleErrors.NotFound);
        }

        var remove = circle.RemoveMember(userId, request.UserId);
        if (remove.IsFailure)
        {
            return remove;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
