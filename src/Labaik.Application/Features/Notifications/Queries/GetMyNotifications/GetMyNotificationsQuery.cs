using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.Queries.GetMyNotifications;

// Newest first. Next page: pass the oldest returned createdAtUtc as Before.
public sealed record GetMyNotificationsQuery(DateTimeOffset? Before, int Take) : IRequest<Result<IReadOnlyList<NotificationDto>>>;

public sealed class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    public GetMyNotificationsQueryValidator()
        => RuleFor(x => x.Take).InclusiveBetween(1, Paging.MaxTake);
}

public sealed class GetMyNotificationsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var query = context.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (request.Before is { } before)
        {
            query = query.Where(n => n.CreatedAtUtc < before);
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        return items.Select(n => n.ToDto()).ToList();
    }
}
