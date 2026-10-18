using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups.Enums;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Common;

// Who supervises whom, across groups.
internal static class SupervisionAccess
{
    // The supervisor leads at least one group in which the pilgrim is a member.
    public static Task<bool> IsSupervisorOfAsync(
        this IAppDbContext context, Guid supervisorId, Guid pilgrimId, CancellationToken cancellationToken)
        => context.Groups.AnyAsync(
            g => g.LeaderId == supervisorId && g.Members.Any(m => m.UserId == pilgrimId && m.Role == GroupMemberRole.Member),
            cancellationToken);

    // Supervisors of every group the pilgrim belongs to (distinct).
    public static Task<List<Guid>> GetSupervisorIdsOfAsync(
        this IAppDbContext context, Guid pilgrimId, CancellationToken cancellationToken)
        => context.Groups
            .Where(g => g.LeaderId != null && g.LeaderId != pilgrimId && g.Members.Any(m => m.UserId == pilgrimId))
            .Select(g => g.LeaderId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
}
