using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Application.Features.Home.Dtos;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Home.Queries.GetHome;

// Read-only aggregation for the home screen: one call instead of three. Never creates data.
// GroupId (optional) picks which of the user's groups the group section shows (supervisors of several
// groups); without it, the most recently joined group. A group the user is not in -> NotFound.
public sealed record GetHomeQuery(Guid? GroupId = null) : IRequest<Result<HomeDto>>;

public sealed class GetHomeQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    ICompanyServiceGate serviceGate) : IRequestHandler<GetHomeQuery, Result<HomeDto>>
{
    public async Task<Result<HomeDto>> Handle(GetHomeQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var journey = await context.Journeys
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.Id)
            .Select(j => new HomeJourneyDto(j.Id, j.PilgrimageType, j.Status, j.IsFirstTime))
            .FirstOrDefaultAsync(cancellationToken);

        var ritual = await GetRitualAsync(userId, journey?.PilgrimageType ?? PilgrimageType.Umrah, cancellationToken);
        var group = await GetGroupAsync(userId, request.GroupId, cancellationToken);
        if (request.GroupId is not null && group is null)
        {
            return GroupErrors.NotFound;
        }

        var unreadNotifications = await context.Notifications
            .CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

        var enabled = new CompanyServicesDto(
            await serviceGate.IsEnabledAsync(userId, s => s.GpsTracking, cancellationToken),
            await serviceGate.IsEnabledAsync(userId, s => s.DuaAndRituals, cancellationToken),
            await serviceGate.IsEnabledAsync(userId, s => s.InstantNotifications, cancellationToken),
            await serviceGate.IsEnabledAsync(userId, s => s.ChatWithGuides, cancellationToken),
            await serviceGate.IsEnabledAsync(userId, s => s.ReportsAndStats, cancellationToken));
        return new HomeDto(journey, ritual, group, unreadNotifications, enabled);
    }

    private async Task<HomeRitualDto?> GetRitualAsync(Guid userId, PilgrimageType type, CancellationToken cancellationToken)
    {
        var steps = await context.GetPublishedStepsAsync(type, cancellationToken);
        if (steps.Count == 0)
        {
            return null;
        }

        var progress = await context.RitualProgresses
            .Include(p => p.CompletedSteps)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PilgrimageType == type, cancellationToken);

        if (progress is null)
        {
            return new HomeRitualDto(type, IsStarted: false, IsCompleted: false, 0, steps.Count, steps[0].ToDto());
        }

        var next = progress.IsCompleted ? null : steps.FirstOrDefault(s => s.Order == progress.CurrentOrder);
        return new HomeRitualDto(
            type, IsStarted: true, progress.IsCompleted, progress.CompletedSteps.Count, steps.Count, next?.ToDto());
    }

    private async Task<HomeGroupDto?> GetGroupAsync(Guid userId, Guid? groupId, CancellationToken cancellationToken)
    {
        // The requested group, else the most recently joined one (a pilgrim normally belongs to one).
        var group = await context.Groups
            .Include(g => g.Members)
            .AsNoTracking()
            .Where(g => g.Members.Any(m => m.UserId == userId) && (groupId == null || g.Id == groupId))
            .OrderByDescending(g => g.Members.Where(m => m.UserId == userId).Max(m => m.JoinedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
        {
            return null;
        }

        var me = group.Members.First(m => m.UserId == userId);
        var myRole = me.Role;
        var leaderName = group.LeaderId is { } leaderId
            ? await userDirectory.GetFullNameAsync(leaderId, cancellationToken)
            : null;

        var unread = await context.Announcements
            .Where(AnnouncementVisibility.VisibleInGroup(group.Id, group.CompanyId))
            .CountAsync(a => !a.Reads.Any(r => r.UserId == userId), cancellationToken);

        var unreadMessages = await context.CountUnreadChatAsync(group.Id, userId, me.ChatLastReadAtUtc, cancellationToken);

        return new HomeGroupDto(group.Id, group.Name, leaderName, group.MemberCount, myRole, unread, group.TripId,
            unreadMessages, group.OnlySupervisorCanPost, me.ChatMutedUntilUtc);
    }
}
