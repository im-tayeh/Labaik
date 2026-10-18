using Labaik.Application.Common.Interfaces;
using Labaik.Domain.HelpCenter;
using Labaik.Domain.Notifications.Enums;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.HelpCenter.Common;

internal static class LostChildAccess
{
    // The reporter, or anyone who was alerted about this report (they hold its notification).
    public static async Task<bool> CanSeeAsync(
        IAppDbContext context, ICurrentUser currentUser, LostChildReport report, CancellationToken cancellationToken)
        => currentUser.Id is { } userId
           && (report.ReporterId == userId
               || await context.Notifications.AnyAsync(
                   n => n.UserId == userId && n.Type == NotificationType.LostChildReported && n.ReferenceId == report.Id,
                   cancellationToken));
}
