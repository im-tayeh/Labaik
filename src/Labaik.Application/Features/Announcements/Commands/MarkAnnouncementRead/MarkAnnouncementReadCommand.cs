using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Announcements;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Commands.MarkAnnouncementRead;

// Idempotent: reading an already-read announcement succeeds without changes.
public sealed record MarkAnnouncementReadCommand(Guid AnnouncementId) : IRequest<Result>;

public sealed class MarkAnnouncementReadCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<MarkAnnouncementReadCommand, Result>
{
    public async Task<Result> Handle(MarkAnnouncementReadCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        var announcement = await context.Announcements
            .Include(a => a.Reads)
            .FirstOrDefaultAsync(a => a.Id == request.AnnouncementId, cancellationToken);

        // Only members of the announcement's group may read it; others get NotFound.
        var isMember = announcement is not null && await context.Groups
            .AnyAsync(g => g.Id == announcement.GroupId && g.Members.Any(m => m.UserId == userId), cancellationToken);

        if (announcement is null || !isMember)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        announcement.MarkReadBy(userId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
