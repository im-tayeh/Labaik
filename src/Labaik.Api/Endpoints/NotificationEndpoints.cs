using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Labaik.Application.Features.Notifications.Commands.MarkNotificationRead;
using Labaik.Application.Features.Notifications.Queries.GetMyNotifications;
using Labaik.Application.Features.Notifications.Queries.GetUnreadNotificationCount;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/notifications")
            .WithApiVersionSet(versionSet)
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct, DateTimeOffset? before, int take = Paging.DefaultTake) =>
        {
            var result = await sender.Send(new GetMyNotificationsQuery(before, take), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("My notifications, newest first (cursor: before = oldest createdAtUtc)");

        group.MapGet("/unread-count", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUnreadNotificationCountQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Number of unread notifications");

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkNotificationReadCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Mark one notification as read (idempotent)");

        group.MapPost("/read-all", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkAllNotificationsReadCommand(), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Mark all my notifications as read");

        return app;
    }
}
