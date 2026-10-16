using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Domain.Announcements;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Commands.PostAnnouncement;

// HTTP body; the group comes from the route.
public sealed record PostAnnouncementRequest(string Title, string Body);

public sealed record PostAnnouncementCommand(Guid GroupId, string Title, string Body) : IRequest<Result<AnnouncementDto>>;

public sealed class PostAnnouncementCommandValidator : AbstractValidator<PostAnnouncementCommand>
{
    public PostAnnouncementCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(AnnouncementErrors.TitleMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(AnnouncementErrors.BodyMaxLength);
    }
}

public sealed class PostAnnouncementCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<PostAnnouncementCommand, Result<AnnouncementDto>>
{
    public async Task<Result<AnnouncementDto>> Handle(PostAnnouncementCommand request, CancellationToken cancellationToken)
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

        var post = group.PostAnnouncement(userId, request.Title, request.Body, timeProvider.GetUtcNow());
        if (post.IsFailure)
        {
            return post.Error!;
        }

        var announcement = post.Value;
        context.Announcements.Add(announcement);
        await context.SaveChangesAsync(cancellationToken);

        var authorName = await userDirectory.GetFullNameAsync(userId, cancellationToken);
        return new AnnouncementDto(
            announcement.Id, announcement.GroupId, announcement.Title, announcement.Body,
            announcement.AuthorId, authorName, announcement.CreatedAtUtc, IsRead: true);
    }
}
