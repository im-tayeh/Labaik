using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Commands.SetGatheringPoint;

// HTTP body; the group comes from the route.
public sealed record SetGatheringPointRequest(string Address, double Latitude, double Longitude, DateTimeOffset? GatheringTimeUtc);

// Group supervisor only (members -> 403, non-members -> 404). Replaces the whole point; members are notified.
public sealed record SetGatheringPointCommand(
    Guid GroupId, string Address, double Latitude, double Longitude, DateTimeOffset? GatheringTimeUtc) : IRequest<Result<GatheringPointDto>>
{
    public static SetGatheringPointCommand From(Guid groupId, SetGatheringPointRequest b) =>
        new(groupId, b.Address, b.Latitude, b.Longitude, b.GatheringTimeUtc);
}

public sealed class SetGatheringPointCommandValidator : AbstractValidator<SetGatheringPointCommand>
{
    public SetGatheringPointCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Address).NotEmpty().MaximumLength(GatheringPoint.AddressMaxLength);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class SetGatheringPointCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SetGatheringPointCommand, Result<GatheringPointDto>>
{
    public async Task<Result<GatheringPointDto>> Handle(SetGatheringPointCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        // Non-members must not learn that the group exists.
        if (group is null || !group.IsMember(userId))
        {
            return GroupErrors.NotFound;
        }

        var set = group.SetGatheringPoint(
            userId, request.Address, request.Latitude, request.Longitude, request.GatheringTimeUtc, timeProvider.GetUtcNow());
        if (set.IsFailure)
        {
            return set.Error!;
        }

        await context.SaveChangesAsync(cancellationToken); // GatheringPointChanged -> member notifications
        return group.GatheringPoint!.ToDto();
    }
}
