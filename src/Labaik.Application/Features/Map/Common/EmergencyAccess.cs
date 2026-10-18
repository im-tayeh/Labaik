using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Map.Common;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Map.Common;

internal static class EmergencyAccess
{
    public static async Task<bool> CanSeeAsync(
        IAppDbContext context, ICurrentUser currentUser, EmergencyAlert alert, CancellationToken cancellationToken)
        => currentUser.Id is { } userId
           && (alert.UserId == userId || await context.IsSupervisorOfAsync(userId, alert.UserId, cancellationToken));
}
