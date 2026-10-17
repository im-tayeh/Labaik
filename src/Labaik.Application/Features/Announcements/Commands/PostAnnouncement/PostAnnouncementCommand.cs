using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Domain.Announcements;
using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Commands.PostAnnouncement;

// HTTP body; the group comes from the route. New fields are optional (importance defaults to Normal).
public sealed record PostAnnouncementRequest(
    string Title,
    string Body,
    AnnouncementImportance? Importance = null,
    DateTimeOffset? GatheringTimeUtc = null,
    string? Location = null,
    string? ImageUrl = null);

public sealed record PostAnnouncementCommand(
    Guid GroupId,
    string Title,
    string Body,
    AnnouncementImportance Importance = AnnouncementImportance.Normal,
    DateTimeOffset? GatheringTimeUtc = null,
    string? Location = null,
    string? ImageUrl = null) : IRequest<Result<AnnouncementDto>>
{
    public static PostAnnouncementCommand From(Guid groupId, PostAnnouncementRequest b) => new(
        groupId, b.Title, b.Body, b.Importance ?? AnnouncementImportance.Normal, b.GatheringTimeUtc, b.Location, b.ImageUrl);

    public AnnouncementContent ToContent() => new(Title, Body, Importance, GatheringTimeUtc, Location, ImageUrl);
}

public sealed class PostAnnouncementCommandValidator : AbstractValidator<PostAnnouncementCommand>
{
    public PostAnnouncementCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(AnnouncementErrors.TitleMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(AnnouncementErrors.BodyMaxLength);
        RuleFor(x => x.Importance).IsInEnum();
        RuleFor(x => x.Location).MaximumLength(AnnouncementErrors.LocationMaxLength);
        RuleFor(x => x.ImageUrl).MaximumLength(AnnouncementErrors.ImageUrlMaxLength);
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

        var post = group.PostAnnouncement(userId, request.ToContent(), timeProvider.GetUtcNow());
        if (post.IsFailure)
        {
            return post.Error!;
        }

        var announcement = post.Value;
        context.Announcements.Add(announcement);
        await context.SaveChangesAsync(cancellationToken);

        var authorName = await userDirectory.GetFullNameAsync(userId, cancellationToken);
        return announcement.ToDto(authorName, isRead: true);
    }
}
