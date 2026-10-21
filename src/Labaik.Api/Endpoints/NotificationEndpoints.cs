using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Api.OpenApi;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Labaik.Application.Features.Notifications.Commands.MarkNotificationRead;
using Labaik.Application.Features.Notifications.Queries.GetMyNotifications;
using Labaik.Application.Features.Notifications.Queries.GetUnreadNotificationCount;
using Labaik.Application.Features.Notifications.Commands.ManageDeviceToken;
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
        })
        .WithName("GetNotifications")
        .WithSummary("My notifications, newest first (cursor: before = oldest createdAtUtc)")
        .Returns<IReadOnlyList<NotificationDto>>().Fails(401).Validates();

        group.MapGet("/unread-count", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUnreadNotificationCountQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetUnreadNotificationCount")
        .WithSummary("Number of unread notifications")
        .Returns<UnreadCountDto>().Fails(401);

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkNotificationReadCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("MarkNotificationRead")
        .WithSummary("Mark one notification as read (idempotent)")
        .ReturnsNoContent().Fails(401, 404);

        group.MapPost("/read-all", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkAllNotificationsReadCommand(), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("MarkAllNotificationsRead")
        .WithSummary("Mark all my notifications as read")
        .ReturnsNoContent().Fails(401);

        group.MapPut("/devices", async (DeviceTokenRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RegisterDeviceTokenCommand(body.Token, body.Platform), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithName("RegisterNotificationDevice")
        .WithSummary("Register or update this device's FCM token")
        .ReturnsNoContent().Fails(400, 401).Validates();

        group.MapDelete("/devices/{token}", async (string token, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveDeviceTokenCommand(token), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithName("RemoveNotificationDevice")
        .WithSummary("Remove this device's FCM token")
        .ReturnsNoContent().Fails(400, 401).Validates();

        return app;
    }
}
