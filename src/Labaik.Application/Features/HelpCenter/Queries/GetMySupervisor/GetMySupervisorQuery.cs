using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.HelpCenter.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.HelpCenter.Queries.GetMySupervisor;

// The supervisor of my group (groupId, else my most recently joined group that has a supervisor).
// Value is null when I am in no supervised group; a groupId I do not belong to -> 404.
public sealed record GetMySupervisorQuery(Guid? GroupId) : IRequest<Result<SupervisorContactDto?>>;

public sealed class GetMySupervisorQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetMySupervisorQuery, Result<SupervisorContactDto?>>
{
    public async Task<Result<SupervisorContactDto?>> Handle(GetMySupervisorQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        if (request.GroupId is { } requested
            && !await context.Groups.AnyAsync(g => g.Id == requested && g.Members.Any(m => m.UserId == userId), cancellationToken))
        {
            return GroupErrors.NotFound;
        }

        var group = await context.Groups
            .AsNoTracking()
            .Where(g => g.LeaderId != null && g.LeaderId != userId
                && g.Members.Any(m => m.UserId == userId && m.Role == GroupMemberRole.Member)
                && (request.GroupId == null || g.Id == request.GroupId))
            .OrderByDescending(g => g.Members.Where(m => m.UserId == userId).Max(m => m.JoinedAtUtc))
            .Select(g => new { g.Id, g.Name, SupervisorId = g.LeaderId!.Value })
            .FirstOrDefaultAsync(cancellationToken);
        if (group is null)
        {
            return Result.Success<SupervisorContactDto?>(null);
        }

        var contact = await userDirectory.GetContactAsync(group.SupervisorId, cancellationToken);
        var photoUrl = await context.PilgrimProfiles
            .Where(p => p.UserId == group.SupervisorId)
            .Select(p => p.PhotoUrl)
            .FirstOrDefaultAsync(cancellationToken);

        return new SupervisorContactDto(
            group.Id, group.Name, group.SupervisorId, contact?.FullName, contact?.Email, contact?.PhoneNumber, photoUrl);
    }
}
