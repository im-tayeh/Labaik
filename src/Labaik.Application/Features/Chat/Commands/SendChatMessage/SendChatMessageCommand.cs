using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Domain.Chat;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Commands.SendChatMessage;

// HTTP body; the group comes from the route. kind omitted = 1 Text (body required);
// kind 2 Location needs latitude + longitude (body = optional caption).
public sealed record SendChatMessageRequest(
    string? Body,
    ChatMessageKind? Kind = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record SendChatMessageCommand(
    Guid GroupId,
    string? Body,
    ChatMessageKind Kind = ChatMessageKind.Text,
    double? Latitude = null,
    double? Longitude = null) : IRequest<Result<ChatMessageDto>>;

public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Body).MaximumLength(ChatErrors.BodyMaxLength);
        RuleFor(x => x.Body).NotEmpty().When(x => x.Kind == ChatMessageKind.Text);
        RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90, 90).When(x => x.Kind == ChatMessageKind.Location);
        RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180, 180).When(x => x.Kind == ChatMessageKind.Location);
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

        var content = new ChatMessageContent(request.Kind, request.Body, request.Latitude, request.Longitude);
        var post = group.PostMessage(userId, content, timeProvider.GetUtcNow());
        if (post.IsFailure)
        {
            return post.Error!;
        }

        var message = post.Value;
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken); // ChatMessageSent -> realtime push (+ notifications)

        var dtos = await new[] { message }.ToDtosAsync(context, userDirectory, userId, null, cancellationToken);
        return dtos[0];
    }
}
