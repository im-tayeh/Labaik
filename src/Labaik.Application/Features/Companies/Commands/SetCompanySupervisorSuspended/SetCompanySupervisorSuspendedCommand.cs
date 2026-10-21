using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Commands.SetCompanySupervisorSuspended;

// A company suspends (or reactivates) a supervisor on its roster. A supervisor suspended by every
// company they are on cannot sign in or refresh (403 Supervisor.Suspended); suspending also revokes
// their refresh tokens so the current session ends when the access token expires.
public sealed record SetCompanySupervisorSuspendedCommand(Guid UserId, bool Suspended) : IRequest<Result>;

public sealed class SetCompanySupervisorSuspendedCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService) : IRequestHandler<SetCompanySupervisorSuspendedCommand, Result>
{
    public async Task<Result> Handle(SetCompanySupervisorSuspendedCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return Result.Failure(companyId.Error!);
        }

        var company = await context.Companies.FirstAsync(c => c.Id == companyId.Value, cancellationToken);
        var change = request.Suspended
            ? company.SuspendSupervisor(request.UserId)
            : company.ReactivateSupervisor(request.UserId);
        if (change.IsFailure)
        {
            return change;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (request.Suspended)
        {
            await refreshTokenService.RevokeAllForUserAsync(request.UserId, cancellationToken);
        }

        return Result.Success();
    }
}
