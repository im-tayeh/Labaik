using Labaik.Application.Common.Events;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups.Events;
using Labaik.Domain.Notifications.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.EventHandlers;

// Company-facing notifications. Recipient: the company's owner account.

// A company group was created -> GroupCreated (preview = group name).
public sealed class CompanyGroupCreatedHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<GroupCreated>>
{
    public async Task Handle(DomainEventNotification<GroupCreated> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        if (e.CompanyId is not { } companyId)
        {
            return; // legacy standalone group
        }

        await CompanyNotifier.NotifyOwnerAsync(
            context, notifier, companyId, NotificationType.GroupCreated, e.GroupId, e.GroupId,
            await CompanyNotifier.GroupNameAsync(context, e.GroupId, cancellationToken),
            timeProvider.GetUtcNow(), cancellationToken);
    }
}

// A supervisor was assigned to a company group -> SupervisorActivated (preview = supervisor name).
public sealed class SupervisorAssignedHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<SupervisorAssigned>>
{
    public async Task Handle(DomainEventNotification<SupervisorAssigned> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        if (e.CompanyId is not { } companyId)
        {
            return;
        }

        await CompanyNotifier.NotifyOwnerAsync(
            context, notifier, companyId, NotificationType.SupervisorActivated, e.GroupId, e.SupervisorId,
            await userDirectory.GetFullNameAsync(e.SupervisorId, cancellationToken),
            timeProvider.GetUtcNow(), cancellationToken);
    }
}

// First pilgrim added to a group with no supervisor -> GroupWithoutSupervisor (preview = group name).
public sealed class GroupAwaitingSupervisorHandler(
    IAppDbContext context,
    IRealtimeNotifier notifier,
    TimeProvider timeProvider) : INotificationHandler<DomainEventNotification<GroupAwaitingSupervisor>>
{
    public async Task Handle(DomainEventNotification<GroupAwaitingSupervisor> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;

        await CompanyNotifier.NotifyOwnerAsync(
            context, notifier, e.CompanyId, NotificationType.GroupWithoutSupervisor, e.GroupId, e.GroupId,
            await CompanyNotifier.GroupNameAsync(context, e.GroupId, cancellationToken),
            timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class MemberAddedBySupervisorHandler(
    IAppDbContext context, IUserDirectory userDirectory, IRealtimeNotifier notifier, TimeProvider timeProvider)
    : INotificationHandler<DomainEventNotification<MemberJoinedGroup>>
{
    public async Task Handle(DomainEventNotification<MemberJoinedGroup> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        if (e.CompanyId is not { } companyId || e.AddedBySupervisorId is null) return;
        await CompanyNotifier.NotifyOwnerAsync(context, notifier, companyId, NotificationType.MemberAddedBySupervisor,
            e.GroupId, e.UserId, await userDirectory.GetFullNameAsync(e.UserId, cancellationToken),
            timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class MemberRemovedBySupervisorHandler(
    IAppDbContext context, IUserDirectory userDirectory, IRealtimeNotifier notifier, TimeProvider timeProvider)
    : INotificationHandler<DomainEventNotification<MemberRemovedFromGroup>>
{
    public async Task Handle(DomainEventNotification<MemberRemovedFromGroup> notification, CancellationToken cancellationToken)
    {
        var e = notification.DomainEvent;
        await CompanyNotifier.NotifyOwnerAsync(context, notifier, e.CompanyId, NotificationType.MemberRemovedBySupervisor,
            e.GroupId, e.UserId, await userDirectory.GetFullNameAsync(e.UserId, cancellationToken),
            timeProvider.GetUtcNow(), cancellationToken);
    }
}

internal static class CompanyNotifier
{
    public static async Task NotifyOwnerAsync(
        IAppDbContext context,
        IRealtimeNotifier notifier,
        Guid companyId,
        NotificationType type,
        Guid groupId,
        Guid referenceId,
        string? preview,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var company = await context.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.OwnerUserId, c.EnabledServices.InstantNotifications })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null || !company.InstantNotifications)
        {
            return;
        }

        await NotificationWriter.CreateAndPushAsync(
            context, notifier, [company.OwnerUserId], type, groupId, referenceId, preview, nowUtc, cancellationToken);
    }

    public static Task<string?> GroupNameAsync(IAppDbContext context, Guid groupId, CancellationToken cancellationToken)
        => context.Groups.Where(g => g.Id == groupId).Select(g => g.Name).FirstOrDefaultAsync(cancellationToken);
}
