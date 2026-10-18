using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Chat.Commands.MarkChatRead;

// "I have read the group chat up to now" (call when the chat screen is opened / scrolled to the bottom).
// Idempotent; the marker never moves back.
public sealed record MarkChatReadCommand(Guid GroupId) : IRequest<Result>;

public sealed class MarkChatReadCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<MarkChatReadCommand, Result>
{
    public async Task<Result> Handle(MarkChatReadCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        var group = await context.GetGroupForMemberAsync(request.GroupId, userId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.NotFound);
        }

        var mark = group.MarkChatRead(userId, timeProvider.GetUtcNow());
        if (mark.IsFailure)
        {
            return mark;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
