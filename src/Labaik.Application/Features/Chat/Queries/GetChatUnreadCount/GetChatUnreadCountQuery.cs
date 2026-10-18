using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Queries.GetChatUnreadCount;

// Group chat messages from others sent after my read marker.
public sealed record GetChatUnreadCountQuery(Guid GroupId) : IRequest<Result<UnreadCountDto>>;

public sealed class GetChatUnreadCountQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetChatUnreadCountQuery, Result<UnreadCountDto>>
{
    public async Task<Result<UnreadCountDto>> Handle(GetChatUnreadCountQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var member = await context.Groups
            .Where(g => g.Id == request.GroupId)
            .SelectMany(g => g.Members.Where(m => m.UserId == userId))
            .Select(m => new { m.ChatLastReadAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (member is null)
        {
            return GroupErrors.NotFound;
        }

        return new UnreadCountDto(
            await context.CountUnreadChatAsync(request.GroupId, userId, member.ChatLastReadAtUtc, cancellationToken));
    }
}
