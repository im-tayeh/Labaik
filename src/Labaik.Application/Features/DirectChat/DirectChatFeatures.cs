using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Notifications.EventHandlers;
using Labaik.Domain.Chat;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.DirectChat;

public sealed record DirectMessageDto(Guid Id, Guid ConversationId, Guid SenderId, string? SenderName,
    string Body, DateTimeOffset SentAtUtc, bool IsRead);
public sealed record ConversationDto(Guid Id, Guid OtherUserId, string OtherName, string? OtherPhotoUrl,
    string? LastMessage, DateTimeOffset? LastMessageAtUtc, int UnreadCount);

public sealed record GetMyConversationsQuery : IRequest<Result<IReadOnlyList<ConversationDto>>>;
public sealed class GetMyConversationsQueryHandler(IAppDbContext context, ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetMyConversationsQuery, Result<IReadOnlyList<ConversationDto>>>
{
    public async Task<Result<IReadOnlyList<ConversationDto>>> Handle(GetMyConversationsQuery request, CancellationToken ct)
    {
        if (currentUser.Id is not { } me) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        // One query for every conversation with its last message and my unread count (no query per row).
        var conversations = await context.Conversations.AsNoTracking()
            .Where(c => c.ParticipantAId == me || c.ParticipantBId == me)
            .OrderByDescending(c => c.LastMessageAtUtc ?? c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id,
                Other = c.ParticipantAId == me ? c.ParticipantBId : c.ParticipantAId,
                c.LastMessageAtUtc,
                LastBody = context.DirectMessages.Where(m => m.ConversationId == c.Id)
                    .OrderByDescending(m => m.SentAtUtc).Select(m => m.Body).FirstOrDefault(),
                Unread = context.DirectMessages.Count(m => m.ConversationId == c.Id && m.SenderId != me
                    && ((c.ParticipantAId == me ? c.ALastReadAtUtc : c.BLastReadAtUtc) == null
                        || m.SentAtUtc > (c.ParticipantAId == me ? c.ALastReadAtUtc : c.BLastReadAtUtc)))
            })
            .ToListAsync(ct);
        var otherIds = conversations.Select(c => c.Other).Distinct().ToList();
        var contacts = await userDirectory.GetContactsAsync(otherIds, ct);
        var photos = await context.PilgrimProfiles.AsNoTracking().Where(p => otherIds.Contains(p.UserId) && p.PhotoUrl != null)
            .ToDictionaryAsync(p => p.UserId, p => p.PhotoUrl, ct);
        return conversations.Select(c => new ConversationDto(c.Id, c.Other,
            contacts.GetValueOrDefault(c.Other)?.FullName ?? string.Empty, photos.GetValueOrDefault(c.Other),
            c.LastBody, c.LastMessageAtUtc, c.Unread)).ToList();
    }
}

public sealed record StartDirectChatRequest(Guid UserId);
public sealed record StartDirectChatResult(ConversationDto Conversation, bool Created);
public sealed record StartDirectChatCommand(Guid UserId) : IRequest<Result<StartDirectChatResult>>;
public sealed class StartDirectChatCommandValidator : AbstractValidator<StartDirectChatCommand>
{ public StartDirectChatCommandValidator() => RuleFor(x => x.UserId).NotEmpty(); }
public sealed class StartDirectChatCommandHandler(IAppDbContext context, ICurrentUser currentUser,
    IUserDirectory userDirectory, TimeProvider timeProvider) : IRequestHandler<StartDirectChatCommand, Result<StartDirectChatResult>>
{
    public async Task<Result<StartDirectChatResult>> Handle(StartDirectChatCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } me) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        if (me == request.UserId) return Error.Validation("Chat.InvalidParticipants", "You cannot start a conversation with yourself.");
        var allowed = await context.IsSupervisorOfAsync(me, request.UserId, ct)
            || await context.IsSupervisorOfAsync(request.UserId, me, ct);
        if (!allowed) return Error.Forbidden("Chat.NotAllowed", "You can only message your supervisor or your pilgrims.");
        var (a, b) = me.CompareTo(request.UserId) < 0 ? (me, request.UserId) : (request.UserId, me);
        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.ParticipantAId == a && c.ParticipantBId == b, ct);
        var wasCreated = conversation is null;
        if (conversation is null)
        {
            var created = Conversation.Start(me, request.UserId, timeProvider.GetUtcNow());
            if (created.IsFailure) return created.Error!;
            conversation = created.Value;
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(ct);
        }
        var other = await userDirectory.GetContactAsync(request.UserId, ct);
        var photo = await context.PilgrimProfiles.AsNoTracking().Where(p => p.UserId == request.UserId)
            .Select(p => p.PhotoUrl).FirstOrDefaultAsync(ct);
        var last = await context.DirectMessages.AsNoTracking().Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.SentAtUtc).FirstOrDefaultAsync(ct);
        var readAt = conversation.LastReadBy(me);
        var unread = await context.DirectMessages.CountAsync(m => m.ConversationId == conversation.Id && m.SenderId != me
            && (readAt == null || m.SentAtUtc > readAt), ct);
        return new StartDirectChatResult(new ConversationDto(conversation.Id, request.UserId, other?.FullName ?? string.Empty,
            photo, last?.Body, conversation.LastMessageAtUtc, unread), wasCreated);
    }
}

public sealed record GetDirectMessagesQuery(Guid ConversationId, DateTimeOffset? Before, int Take)
    : IRequest<Result<IReadOnlyList<DirectMessageDto>>>;
public sealed class GetDirectMessagesQueryValidator : AbstractValidator<GetDirectMessagesQuery>
{ public GetDirectMessagesQueryValidator() { RuleFor(x => x.ConversationId).NotEmpty(); RuleFor(x => x.Take).InclusiveBetween(1, Paging.MaxTake); } }
public sealed class GetDirectMessagesQueryHandler(IAppDbContext context, ICurrentUser currentUser, IUserDirectory userDirectory)
    : IRequestHandler<GetDirectMessagesQuery, Result<IReadOnlyList<DirectMessageDto>>>
{
    public async Task<Result<IReadOnlyList<DirectMessageDto>>> Handle(GetDirectMessagesQuery request, CancellationToken ct)
    {
        if (currentUser.Id is not { } me) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        var conversation = await context.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);
        if (conversation is null || !conversation.Has(me)) return Error.NotFound("Chat.ConversationNotFound", "The conversation was not found.");
        var query = context.DirectMessages.AsNoTracking().Where(m => m.ConversationId == request.ConversationId);
        if (request.Before is { } before) query = query.Where(m => m.SentAtUtc < before);
        var messages = await query.OrderByDescending(m => m.SentAtUtc).ThenByDescending(m => m.Id).Take(request.Take).ToListAsync(ct);
        var names = await userDirectory.GetFullNamesAsync(messages.Select(m => m.SenderId).Distinct().ToList(), ct);
        var readAt = conversation.LastReadBy(me);
        return messages.Select(m => new DirectMessageDto(m.Id, m.ConversationId, m.SenderId, names.GetValueOrDefault(m.SenderId),
            m.Body, m.SentAtUtc, m.SenderId == me || (readAt is { } r && m.SentAtUtc <= r))).ToList();
    }
}

public sealed record SendDirectMessageRequest(string Body);
public sealed record SendDirectMessageCommand(Guid ConversationId, string Body) : IRequest<Result<DirectMessageDto>>;
public sealed class SendDirectMessageCommandValidator : AbstractValidator<SendDirectMessageCommand>
{ public SendDirectMessageCommandValidator() { RuleFor(x => x.ConversationId).NotEmpty(); RuleFor(x => x.Body).NotEmpty().MaximumLength(ChatErrors.BodyMaxLength); } }
public sealed class SendDirectMessageCommandHandler(IAppDbContext context, ICurrentUser currentUser,
    IUserDirectory userDirectory, IRealtimeNotifier notifier, TimeProvider timeProvider)
    : IRequestHandler<SendDirectMessageCommand, Result<DirectMessageDto>>
{
    public async Task<Result<DirectMessageDto>> Handle(SendDirectMessageCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } me) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);
        if (conversation is null || !conversation.Has(me)) return Error.NotFound("Chat.ConversationNotFound", "The conversation was not found.");
        // Same rule as starting a chat, checked on every message: a pilgrim moved out of the group (or a
        // supervisor replaced) can no longer write in the old conversation, though its history stays readable.
        var other = conversation.OtherThan(me);
        var supervisorSent = await context.IsSupervisorOfAsync(me, other, ct);
        if (!supervisorSent && !await context.IsSupervisorOfAsync(other, me, ct))
            return Error.Forbidden("Chat.NotAllowed", "You can only message your supervisor or your pilgrims.");
        var now = timeProvider.GetUtcNow();
        var sent = conversation.Post(me, request.Body, now);
        if (sent.IsFailure) return sent.Error!;
        context.DirectMessages.Add(sent.Value);
        await context.SaveChangesAsync(ct);
        var dto = new DirectMessageDto(sent.Value.Id, sent.Value.ConversationId, me,
            await userDirectory.GetFullNameAsync(me, ct), sent.Value.Body, sent.Value.SentAtUtc, false);
        await notifier.DirectMessageReceivedAsync(conversation.OtherThan(me), dto, ct);
        var notificationType = supervisorSent ? NotificationType.SupervisorMessagePosted : NotificationType.DirectMessageReceived;
        await NotificationWriter.CreateAndPushAsync(context, notifier, [conversation.OtherThan(me)], notificationType,
            null, dto.Id, dto.Body, now, ct);
        return dto;
    }
}

public sealed record MarkDirectChatReadCommand(Guid ConversationId) : IRequest<Result>;
public sealed class MarkDirectChatReadCommandHandler(IAppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<MarkDirectChatReadCommand, Result>
{
    public async Task<Result> Handle(MarkDirectChatReadCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } me) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);
        if (conversation is null || !conversation.Has(me)) return Result.Failure(Error.NotFound("Chat.ConversationNotFound", "The conversation was not found."));
        var result = conversation.MarkRead(me, timeProvider.GetUtcNow());
        if (result.IsFailure) return result;
        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
