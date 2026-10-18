using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Map.Common;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Commands.ReportEmergency;

// Field-mode SOS. The supervisors of my groups get notification type 10 (referenceId = alert id).
public sealed record ReportEmergencyCommand(double Latitude, double Longitude, double? AccuracyMeters, string? Note)
    : IRequest<Result<EmergencyAlertDto>>;

public sealed class ReportEmergencyCommandValidator : AbstractValidator<ReportEmergencyCommand>
{
    public ReportEmergencyCommandValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0).When(x => x.AccuracyMeters is not null);
        RuleFor(x => x.Note).MaximumLength(MapErrors.NoteMaxLength);
    }
}

public sealed class ReportEmergencyCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<ReportEmergencyCommand, Result<EmergencyAlertDto>>
{
    public async Task<Result<EmergencyAlertDto>> Handle(ReportEmergencyCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var create = EmergencyAlert.Create(
            userId, request.Latitude, request.Longitude, request.AccuracyMeters, request.Note, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.EmergencyAlerts.Add(create.Value);
        await context.SaveChangesAsync(cancellationToken); // EmergencyReported -> supervisors notified

        return create.Value.ToDto(await userDirectory.GetFullNameAsync(userId, cancellationToken));
    }
}

