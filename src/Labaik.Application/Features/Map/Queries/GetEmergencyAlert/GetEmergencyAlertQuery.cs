using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Map.Common;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Queries.GetEmergencyAlert;

// The reporter or a supervisor of one of the reporter's groups; anyone else gets 404.
public sealed record GetEmergencyAlertQuery(Guid AlertId) : IRequest<Result<EmergencyAlertDto>>;

public sealed class GetEmergencyAlertQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetEmergencyAlertQuery, Result<EmergencyAlertDto>>
{
    public async Task<Result<EmergencyAlertDto>> Handle(GetEmergencyAlertQuery request, CancellationToken cancellationToken)
    {
        var alert = await context.EmergencyAlerts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken);
        if (alert is null || !await EmergencyAccess.CanSeeAsync(context, currentUser, alert, cancellationToken))
        {
            return MapErrors.EmergencyNotFound;
        }

        return alert.ToDto(await userDirectory.GetFullNameAsync(alert.UserId, cancellationToken));
    }
}

