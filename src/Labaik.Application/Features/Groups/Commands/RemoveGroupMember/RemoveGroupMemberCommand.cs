using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.RemoveGroupMember;

// A company removes a pilgrim from one of its groups (the supervisor is replaced, not removed).
public sealed record RemoveGroupMemberCommand(Guid GroupId, Guid UserId) : IRequest<Result>;

public sealed class RemoveGroupMemberCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<RemoveGroupMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return Result.Failure(companyId.Error!);
        }

        var group = await context.GetCompanyGroupAsync(request.GroupId, companyId.Value, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.NotFound);
        }

        var remove = group.RemoveMember(request.UserId);
        if (remove.IsFailure)
        {
            return remove;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
