using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Application.Features.Companies.Common;
using Labaik.Domain.Announcements;
using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Announcements.Commands.PostCompanyAnnouncement;

// Company account broadcasts to every pilgrim in all its groups (Audience = AllCompanyPilgrims).
public sealed record PostCompanyAnnouncementCommand(
    string Title,
    string Body,
    AnnouncementImportance? Importance = null,
    DateTimeOffset? GatheringTimeUtc = null,
    string? Location = null,
    string? ImageUrl = null) : IRequest<Result<AnnouncementDto>>;

public sealed class PostCompanyAnnouncementCommandValidator : AbstractValidator<PostCompanyAnnouncementCommand>
{
    public PostCompanyAnnouncementCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(AnnouncementErrors.TitleMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(AnnouncementErrors.BodyMaxLength);
        RuleFor(x => x.Importance).IsInEnum().When(x => x.Importance is not null);
        RuleFor(x => x.Location).MaximumLength(AnnouncementErrors.LocationMaxLength);
        RuleFor(x => x.ImageUrl).MaximumLength(AnnouncementErrors.ImageUrlMaxLength);
    }
}

public sealed class PostCompanyAnnouncementCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<PostCompanyAnnouncementCommand, Result<AnnouncementDto>>
{
    public async Task<Result<AnnouncementDto>> Handle(PostCompanyAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var company = await context.Companies.AsNoTracking()
            .FirstAsync(c => c.Id == companyId.Value, cancellationToken);

        var content = new AnnouncementContent(request.Title, request.Body,
            request.Importance ?? AnnouncementImportance.Normal, request.GatheringTimeUtc, request.Location, request.ImageUrl);

        var post = company.PostAnnouncement(currentUser.Id!.Value, content, timeProvider.GetUtcNow());
        if (post.IsFailure)
        {
            return post.Error!;
        }

        context.Announcements.Add(post.Value);
        await context.SaveChangesAsync(cancellationToken); // AnnouncementPosted -> every company pilgrim

        var authorName = await userDirectory.GetFullNameAsync(post.Value.AuthorId, cancellationToken);
        return post.Value.ToDto(authorName, isRead: true);
    }
}
