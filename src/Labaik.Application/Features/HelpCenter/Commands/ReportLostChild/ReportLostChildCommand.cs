using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.HelpCenter.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.HelpCenter;
using MediatR;

namespace Labaik.Application.Features.HelpCenter.Commands.ReportLostChild;

// POST /lost-child-reports. My supervisors are always alerted (type 11); with shareWithNearby, app users
// sharing a fresh location within 1 km are alerted too.
public sealed record ReportLostChildCommand(
    int ApproxAge,
    string Description,
    string? PhotoUrl,
    double Latitude,
    double Longitude,
    string? LocationDescription,
    bool ShareWithNearby = false) : IRequest<Result<LostChildReportDto>>;

public sealed class ReportLostChildCommandValidator : AbstractValidator<ReportLostChildCommand>
{
    public ReportLostChildCommandValidator()
    {
        RuleFor(x => x.ApproxAge).InclusiveBetween(0, LostChildReport.MaxAge);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(LostChildReport.DescriptionMaxLength);
        RuleFor(x => x.PhotoUrl)
            .Must(url => string.IsNullOrWhiteSpace(url)
                || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Must be an absolute https URL.")
            .MaximumLength(LostChildReport.PhotoUrlMaxLength);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.LocationDescription).MaximumLength(LostChildReport.LocationDescriptionMaxLength);
    }
}

public sealed class ReportLostChildCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<ReportLostChildCommand, Result<LostChildReportDto>>
{
    public async Task<Result<LostChildReportDto>> Handle(ReportLostChildCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var create = LostChildReport.Create(
            userId, request.ApproxAge, request.Description, request.PhotoUrl, request.Latitude, request.Longitude,
            request.LocationDescription, request.ShareWithNearby, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.LostChildReports.Add(create.Value);
        await context.SaveChangesAsync(cancellationToken); // LostChildReported -> alerts

        return create.Value.ToDto(await userDirectory.GetFullNameAsync(userId, cancellationToken));
    }
}
