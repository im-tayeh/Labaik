using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Domain.HelpCenter.Events;
using Labaik.Domain.Locations;
using Labaik.Domain.Map;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// Lost child -> the reporter's supervisors, plus (when shared) every app user currently sharing a fresh
// location within NearbyMeters of the report. referenceId = report id, preview = description.
public sealed class LostChildReportedHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<LostChildReported>>
{
    public const double NearbyMeters = 1000;
    private const double BoxDegrees = 0.01; // ~1.1 km of latitude: cheap SQL pre-filter before the exact distance

    public async Task Handle(DomainEventNotification<LostChildReported> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        var report = await context.LostChildReports.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == e.ReportId, cancellationToken);
        if (report is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var recipients = new HashSet<Guid>(await context.GetSupervisorIdsOfAsync(e.ReporterId, cancellationToken));

        if (report.ShareWithNearby)
        {
            var freshSince = now - LocationShare.StaleAfter;
            var candidates = await context.LocationShares.AsNoTracking()
                .Where(s => s.IsSharing && s.UserId != e.ReporterId && s.UpdatedAtUtc >= freshSince
                    && s.Latitude >= report.Latitude - BoxDegrees && s.Latitude <= report.Latitude + BoxDegrees
                    && s.Longitude >= report.Longitude - BoxDegrees && s.Longitude <= report.Longitude + BoxDegrees)
                .Select(s => new { s.UserId, s.Latitude, s.Longitude })
                .ToListAsync(cancellationToken);

            recipients.UnionWith(candidates
                .Where(c => Geo.DistanceMeters(report.Latitude, report.Longitude, c.Latitude!.Value, c.Longitude!.Value) <= NearbyMeters)
                .Select(c => c.UserId));
        }

        recipients.Remove(e.ReporterId);
        await NotificationWriter.CreateAndPushAsync(
            context, notifier, recipients.ToList(), NotificationType.LostChildReported,
            groupId: null, report.Id, report.Description, now, cancellationToken);
    }
}
