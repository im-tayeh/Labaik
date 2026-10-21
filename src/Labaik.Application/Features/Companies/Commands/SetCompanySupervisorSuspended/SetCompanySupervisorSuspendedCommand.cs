using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Notifications.EventHandlers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Commands.SetCompanySupervisorSuspended;

// A company suspends (or reactivates) a supervisor on its roster. A supervisor suspended by every
// company they are on cannot sign in or refresh (403 Supervisor.Suspended); suspending also revokes
// their refresh tokens, and the access token they hold stops working on the next request (AccountBlockMiddleware).
// The company owner and the supervisor get notification 16 (suspended) or 17 (reactivated).
public sealed record SetCompanySupervisorSuspendedCommand(Guid UserId, bool Suspended) : IRequest<Result>;

public sealed class SetCompanySupervisorSuspendedCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IRefreshTokenService refreshTokenService,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider,
    IAccountAccessCache accessCache) : IRequestHandler<SetCompanySupervisorSuspendedCommand, Result>
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
        accessCache.Reset();

        if (request.Suspended)
        {
            await refreshTokenService.RevokeAllForUserAsync(request.UserId, cancellationToken);
        }

        if (company.EnabledServices.InstantNotifications)
        {
            var type = request.Suspended ? NotificationType.SupervisorSuspended : NotificationType.SupervisorReactivated;
            var now = timeProvider.GetUtcNow();
            var supervisorName = await userDirectory.GetFullNameAsync(request.UserId, cancellationToken);
            await NotificationWriter.CreateAndPushAsync(
                context, notifier, [company.OwnerUserId], type, null, request.UserId, supervisorName, now, cancellationToken);
            await NotificationWriter.CreateAndPushAsync(
                context, notifier, [request.UserId], type, null, company.Id, company.Name, now, cancellationToken);
        }

        return Result.Success();
    }
}
