using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Chat;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Chat.Commands;

public sealed record DeleteChatMessageCommand(Guid GroupId, Guid MessageId) : IRequest<Result>;
public sealed class DeleteChatMessageCommandHandler(IAppDbContext context, ICurrentUser currentUser,
    IRealtimeNotifier notifier, TimeProvider timeProvider) : IRequestHandler<DeleteChatMessageCommand, Result>
{
    public async Task<Result> Handle(DeleteChatMessageCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var group = await context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == request.GroupId, ct);
        var message = await context.ChatMessages.FirstOrDefaultAsync(m => m.Id == request.MessageId && m.GroupId == request.GroupId, ct);
        if (group is null || message is null) return Result.Failure(ChatErrors.MessageNotFound);
        var deleted = group.DeleteMessage(userId, message, timeProvider.GetUtcNow());
        if (deleted.IsFailure) return deleted;
        await context.SaveChangesAsync(ct);
        await notifier.ChatMessageDeletedAsync(group.Id, message.Id, ct);
        return Result.Success();
    }
}

public sealed record MuteChatMemberRequest(Guid UserId, int Minutes);
public sealed record MuteChatMemberCommand(Guid GroupId, Guid UserId, int Minutes) : IRequest<Result>;
public sealed class MuteChatMemberCommandValidator : AbstractValidator<MuteChatMemberCommand>
{
    public MuteChatMemberCommandValidator() { RuleFor(x => x.GroupId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Minutes).InclusiveBetween(0, 10080); }
}
public sealed class MuteChatMemberCommandHandler(IAppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<MuteChatMemberCommand, Result>
{
    public async Task<Result> Handle(MuteChatMemberCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var group = await context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == request.GroupId, ct);
        if (group is null) return Result.Failure(GroupErrors.NotFound);
        var result = group.MuteChatMember(userId, request.UserId, request.Minutes, timeProvider.GetUtcNow());
        if (result.IsFailure) return result;
        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record SetChatSettingsRequest(bool OnlySupervisorCanPost);
public sealed record SetChatSettingsCommand(Guid GroupId, bool OnlySupervisorCanPost) : IRequest<Result>;
public sealed class SetChatSettingsCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<SetChatSettingsCommand, Result>
{
    public async Task<Result> Handle(SetChatSettingsCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var group = await context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == request.GroupId, ct);
        if (group is null) return Result.Failure(GroupErrors.NotFound);
        var result = group.SetChatSettings(userId, request.OnlySupervisorCanPost);
        if (result.IsFailure) return result;
        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
