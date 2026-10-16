using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery : IRequest<Result<UnreadCountDto>>;

public sealed class GetUnreadNotificationCountQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetUnreadNotificationCountQuery, Result<UnreadCountDto>>
{
    public async Task<Result<UnreadCountDto>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var count = await context.Notifications
            .CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

        return new UnreadCountDto(count);
    }
}
