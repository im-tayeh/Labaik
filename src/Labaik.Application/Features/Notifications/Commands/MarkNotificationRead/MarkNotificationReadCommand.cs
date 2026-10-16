using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.Commands.MarkNotificationRead;

// Idempotent.
public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

        // Owner only; someone else's notification is reported as not found.
        if (notification is null || notification.UserId != userId)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", "The notification was not found."));
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
