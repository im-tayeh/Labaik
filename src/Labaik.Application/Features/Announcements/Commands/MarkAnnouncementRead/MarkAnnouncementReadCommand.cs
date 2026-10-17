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

        // Only those who can see it may read it: members of its group, or (company-wide) members of
        // any of its company's groups. Others get NotFound.
        var canSee = announcement switch
        {
            null => false,
            { GroupId: { } groupId } => await context.Groups.AnyAsync(
                g => g.Id == groupId && g.Members.Any(m => m.UserId == userId), cancellationToken),
            { CompanyId: { } companyId } => await context.Groups.AnyAsync(
                g => g.CompanyId == companyId && g.Members.Any(m => m.UserId == userId), cancellationToken),
            _ => false
        };

        if (announcement is null || !canSee)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        announcement.MarkReadBy(userId, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
