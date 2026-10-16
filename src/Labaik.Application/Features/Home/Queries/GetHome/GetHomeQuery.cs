using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Home.Dtos;
using Labaik.Application.Features.Rituals.Common;
using Labaik.Application.Features.Rituals.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Home.Queries.GetHome;

// Read-only aggregation for the home screen: one call instead of three. Never creates data.
public sealed record GetHomeQuery : IRequest<Result<HomeDto>>;

public sealed class GetHomeQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetHomeQuery, Result<HomeDto>>
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
        var group = await GetGroupAsync(userId, cancellationToken);

        return new HomeDto(journey, ritual, group);
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

    private async Task<HomeGroupDto?> GetGroupAsync(Guid userId, CancellationToken cancellationToken)
    {
        // The most recently joined group (a pilgrim normally belongs to one).
        var group = await context.Groups
            .Include(g => g.Members)
            .AsNoTracking()
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.Members.Where(m => m.UserId == userId).Max(m => m.JoinedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
        {
            return null;
        }

        var myRole = group.Members.First(m => m.UserId == userId).Role;
        var leaderName = await userDirectory.GetFullNameAsync(group.LeaderId, cancellationToken);

        var unread = await context.Announcements
            .CountAsync(a => a.GroupId == group.Id && !a.Reads.Any(r => r.UserId == userId), cancellationToken);

        return new HomeGroupDto(group.Id, group.Name, leaderName, group.MemberCount, myRole, unread);
    }
}
