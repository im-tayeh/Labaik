using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Domain.Announcements.Events;
using Labaik.Domain.Groups.Events;
using Labaik.Domain.Notifications;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// New announcement -> notify every member except the author.
public sealed class AnnouncementPostedHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<AnnouncementPosted>>
{
    public async Task Handle(DomainEventNotification<AnnouncementPosted> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        var title = await context.Announcements
            .Where(a => a.Id == e.AnnouncementId)
            .Select(a => a.Title)
            .FirstOrDefaultAsync(cancellationToken);

        var recipients = await context.Groups
            .Where(g => g.Id == e.GroupId)
            .SelectMany(g => g.Members.Select(m => m.UserId))
            .Where(userId => userId != e.AuthorId)
            .ToListAsync(cancellationToken);

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, recipients, NotificationType.AnnouncementPosted,
            e.GroupId, e.AnnouncementId, title, timeProvider.GetUtcNow(), cancellationToken);
    }
}

// New member -> notify the group leader.
public sealed class MemberJoinedGroupHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<MemberJoinedGroup>>
{
    public async Task Handle(DomainEventNotification<MemberJoinedGroup> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        var leaderId = await context.Groups
            .Where(g => g.Id == e.GroupId)
            .Select(g => (Guid?)g.LeaderId)
            .FirstOrDefaultAsync(cancellationToken);
        if (leaderId is null || leaderId == e.UserId)
        {
            return;
        }

        var memberName = await userDirectory.GetFullNameAsync(e.UserId, cancellationToken);

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, [leaderId.Value], NotificationType.MemberJoinedGroup,
            e.GroupId, e.UserId, memberName, timeProvider.GetUtcNow(), cancellationToken);
    }
}

internal static class NotificationWriter
{
    public static async Task CreateAndPushAsync(
        IAppDbContext context,
        IRealtimeNotifier notifier,
        IReadOnlyList<Guid> recipients,
        NotificationType type,
        Guid? groupId,
        Guid? referenceId,
        string? preview,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var created = new List<Notification>();
        foreach (var userId in recipients)
        {
            var create = Notification.Create(userId, type, groupId, referenceId, preview, nowUtc);
            if (create.IsSuccess)
            {
                created.Add(create.Value);
            }
        }

        context.Notifications.AddRange(created);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var n in created)
        {
            await notifier.NotificationCreatedAsync(n.UserId, n.ToDto(), cancellationToken);
        }
    }
}
