using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Queries.GetGroupAnnouncements;

// Newest first, with the current user's read state.
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

        var isMember = await context.Groups
            .AnyAsync(g => g.Id == request.GroupId && g.Members.Any(m => m.UserId == userId), cancellationToken);
        if (!isMember)
        {
            return GroupErrors.NotFound;
        }

        var items = await context.Announcements
            .AsNoTracking()
            .Where(a => a.GroupId == request.GroupId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new
            {
                a.Id, a.GroupId, a.Title, a.Body, a.AuthorId, a.CreatedAtUtc,
                IsRead = a.Reads.Any(r => r.UserId == userId)
            })
            .ToListAsync(cancellationToken);

        var authorNames = new Dictionary<Guid, string?>();
        foreach (var authorId in items.Select(i => i.AuthorId).Distinct())
        {
            authorNames[authorId] = await userDirectory.GetFullNameAsync(authorId, cancellationToken);
        }

        return items
            .Select(i => new AnnouncementDto(
                i.Id, i.GroupId, i.Title, i.Body, i.AuthorId, authorNames[i.AuthorId], i.CreatedAtUtc, i.IsRead))
            .ToList();
    }
}
