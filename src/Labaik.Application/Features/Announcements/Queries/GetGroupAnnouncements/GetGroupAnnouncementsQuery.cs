using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Queries.GetGroupAnnouncements;

// Newest first, with the current user's read state: the group's own announcements plus its
// company's company-wide ones.
public sealed record GetGroupAnnouncementsQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<AnnouncementDto>>>;

public sealed class GetGroupAnnouncementsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetGroupAnnouncementsQuery, Result<IReadOnlyList<AnnouncementDto>>>
{
    public async Task<Result<IReadOnlyList<AnnouncementDto>>> Handle(GetGroupAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.Id == request.GroupId && g.Members.Any(m => m.UserId == userId))
            .Select(g => new { g.Id, g.CompanyId })
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var items = await context.Announcements
            .AsNoTracking()
            .Where(AnnouncementVisibility.VisibleInGroup(group.Id, group.CompanyId))
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new { Announcement = a, IsRead = a.Reads.Any(r => r.UserId == userId) })
            .ToListAsync(cancellationToken);

        var authorNames = await userDirectory.GetFullNamesAsync(
            items.Select(i => i.Announcement.AuthorId).Distinct().ToList(), cancellationToken);

        return items
            .Select(i => i.Announcement.ToDto(authorNames.GetValueOrDefault(i.Announcement.AuthorId), i.IsRead))
            .ToList();
    }
}
