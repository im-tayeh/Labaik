using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Map.Common;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Commands.ResolveEmergency;

// Mark handled (idempotent): the reporter or one of their supervisors.
public sealed record ResolveEmergencyCommand(Guid AlertId) : IRequest<Result<EmergencyAlertDto>>;

public sealed class ResolveEmergencyCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<ResolveEmergencyCommand, Result<EmergencyAlertDto>>
{
    public async Task<Result<EmergencyAlertDto>> Handle(ResolveEmergencyCommand request, CancellationToken cancellationToken)
    {
        var alert = await context.EmergencyAlerts.FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken);
        if (alert is null || !await EmergencyAccess.CanSeeAsync(context, currentUser, alert, cancellationToken))
        {
            return MapErrors.EmergencyNotFound;
        }

        alert.Resolve(currentUser.Id!.Value, timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        return alert.ToDto(await userDirectory.GetFullNameAsync(alert.UserId, cancellationToken));
    }
}

