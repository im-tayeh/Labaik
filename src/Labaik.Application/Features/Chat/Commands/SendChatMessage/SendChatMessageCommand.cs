using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Domain.Chat;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Commands.SendChatMessage;

// HTTP body; the group comes from the route.
public sealed record SendChatMessageRequest(string Body);

public sealed record SendChatMessageCommand(Guid GroupId, string Body) : IRequest<Result<ChatMessageDto>>;

public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(ChatErrors.BodyMaxLength);
    }
}

public sealed class SendChatMessageCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<SendChatMessageCommand, Result<ChatMessageDto>>
{
    public async Task<Result<ChatMessageDto>> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.Groups
            .Include(g => g.Members)
            .AsNoTracking() // the group itself is not modified
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);

        // Non-members must not learn that the group exists.
        if (group is null || !group.IsMember(userId))
        {
            return GroupErrors.NotFound;
        }

        var post = group.PostMessage(userId, request.Body, timeProvider.GetUtcNow());
        if (post.IsFailure)
        {
            return post.Error!;
        }

        var message = post.Value;
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken); // ChatMessageSent -> realtime push

        var senderName = await userDirectory.GetFullNameAsync(userId, cancellationToken);
        return new ChatMessageDto(message.Id, message.GroupId, message.SenderId, senderName, message.Body, message.SentAtUtc);
    }
}
