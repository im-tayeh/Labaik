using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.SupervisorRemoveMember;

public sealed record SupervisorRemoveMemberCommand(Guid GroupId, Guid UserId) : IRequest<Result>;

public sealed class SupervisorRemoveMemberCommandValidator : AbstractValidator<SupervisorRemoveMemberCommand>
{
    public SupervisorRemoveMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class SupervisorRemoveMemberCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<SupervisorRemoveMemberCommand, Result>
{
    public async Task<Result> Handle(SupervisorRemoveMemberCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } supervisorId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var group = await context.GetGroupForMemberAsync(request.GroupId, supervisorId, cancellationToken);
        if (group is null) return Result.Failure(GroupErrors.NotFound);
        if (group.LeaderId != supervisorId) return Result.Failure(GroupErrors.NotLeader);

        var removed = group.RemoveMember(request.UserId, supervisorId);
        if (removed.IsFailure) return removed;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
