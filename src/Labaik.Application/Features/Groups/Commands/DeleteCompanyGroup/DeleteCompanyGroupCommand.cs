using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Commands.DeleteCompanyGroup;

public sealed record DeleteCompanyGroupCommand(Guid GroupId) : IRequest<Result>;

public sealed class DeleteCompanyGroupCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<DeleteCompanyGroupCommand, Result>
{
    public async Task<Result> Handle(DeleteCompanyGroupCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return Result.Failure(companyId.Error!);
        var group = await context.GetCompanyGroupAsync(request.GroupId, companyId.Value, cancellationToken);
        if (group is null) return Result.Failure(GroupErrors.NotFound);
        if (group.PilgrimCount > 0) return Result.Failure(GroupErrors.HasMembers);
        await context.ChatMessages.Where(m => m.GroupId == group.Id).ExecuteDeleteAsync(cancellationToken);
        await context.Announcements.Where(a => a.GroupId == group.Id).ExecuteDeleteAsync(cancellationToken);
        context.Groups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
