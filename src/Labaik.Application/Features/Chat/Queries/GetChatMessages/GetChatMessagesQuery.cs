using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Queries.GetChatMessages;

// Newest first. Next page: pass the oldest returned sentAtUtc as Before.
public sealed record GetChatMessagesQuery(Guid GroupId, DateTimeOffset? Before, int Take) : IRequest<Result<IReadOnlyList<ChatMessageDto>>>;

public sealed class GetChatMessagesQueryValidator : AbstractValidator<GetChatMessagesQuery>
{
    public GetChatMessagesQueryValidator()
        => RuleFor(x => x.Take).InclusiveBetween(1, Paging.MaxTake);
}

public sealed class GetChatMessagesQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetChatMessagesQuery, Result<IReadOnlyList<ChatMessageDto>>>
{
    public async Task<Result<IReadOnlyList<ChatMessageDto>>> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
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

        var query = context.ChatMessages.AsNoTracking().Where(m => m.GroupId == request.GroupId);
        if (request.Before is { } before)
        {
            query = query.Where(m => m.SentAtUtc < before);
        }

        var messages = await query
            .OrderByDescending(m => m.SentAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        return Result.Success(await messages.ToDtosAsync(
            context, userDirectory, userId, member.ChatLastReadAtUtc, cancellationToken));
    }
}
