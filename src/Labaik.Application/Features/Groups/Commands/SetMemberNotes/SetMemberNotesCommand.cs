using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.SetMemberNotes;

// HTTP body; group and member come from the route. Null or empty clears the notes.
public sealed record SetMemberNotesRequest(string? Notes);

// Group supervisor only (members -> 403, non-members -> 404).
public sealed record SetMemberNotesCommand(Guid GroupId, Guid UserId, string? Notes) : IRequest<Result>;

public sealed class SetMemberNotesCommandValidator : AbstractValidator<SetMemberNotesCommand>
{
    public SetMemberNotesCommandValidator() => RuleFor(x => x.Notes).MaximumLength(GroupMember.NotesMaxLength);
}

public sealed class SetMemberNotesCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<SetMemberNotesCommand, Result>
{
    public async Task<Result> Handle(SetMemberNotesCommand request, CancellationToken cancellationToken)
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

        var set = group.SetMemberNotes(userId, request.UserId, request.Notes);
        if (set.IsFailure)
        {
            return set;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
