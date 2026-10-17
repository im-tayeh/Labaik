using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Announcements.Commands.PostAnnouncement;
using Labaik.Application.Features.Announcements.Queries.GetGroupAnnouncements;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Chat.Commands.SendChatMessage;
using Labaik.Application.Features.Chat.Queries.GetChatMessages;
using Labaik.Application.Features.Groups.Commands.Attendance;
using Labaik.Application.Features.Groups.Commands.CreateGroup;
using Labaik.Application.Features.Groups.Commands.JoinGroup;
using Labaik.Application.Features.Groups.Commands.SetGatheringPoint;
using Labaik.Application.Features.Groups.Commands.SetMemberNotes;
using Labaik.Application.Features.Groups.Queries.GetGroupMember;
using Labaik.Application.Features.Groups.Queries.GetGatheringPoint;
using Labaik.Application.Features.Groups.Queries.GetGroupAttendance;
using Labaik.Application.Features.Groups.Queries.GetGroupByCode;
using Labaik.Application.Features.Locations.Queries.GetGroupMemberLocations;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/groups")
            .WithApiVersionSet(versionSet)
            .WithTags("Groups")
            .RequireAuthorization();

        group.MapPost("/", async (CreateGroupCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .RequireAuthorization(AuthorizationPolicies.Admin)
        .WithSummary("Create a standalone group (admin only; companies use /company/trips/{tripId}/groups)");

        group.MapGet("/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupByCodeQuery(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Preview a group by its join code");

        group.MapPost("/{code}/join", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new JoinGroupCommand(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .RequireAuthorization(AuthorizationPolicies.Admin)
        .WithSummary("Join a group by its code (admin only; companies add members via /company/groups/{groupId}/members)");

        group.MapPost("/{groupId:guid}/announcements", async (Guid groupId, PostAnnouncementRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new PostAnnouncementCommand(groupId, body.Title, body.Body), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/groups/{groupId}/announcements", result.Value)
                : result.Error!.ToProblem();
        }).WithSummary("Broadcast an announcement to the group (leader only)");

        group.MapGet("/{groupId:guid}/announcements", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupAnnouncementsQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("List the group's announcements, newest first, with read state (members only)");

        group.MapGet("/{groupId:guid}/locations", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupMemberLocationsQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Where are my group members: every member with their shared position (members only)");

        group.MapPut("/{groupId:guid}/gathering-point", async (Guid groupId, SetGatheringPointRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(SetGatheringPointCommand.From(groupId, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Set or move the group's gathering point (group supervisor only; members are notified)");

        group.MapGet("/{groupId:guid}/gathering-point", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGatheringPointQuery(groupId), ct);
            if (result.IsFailure)
            {
                return result.Error!.ToProblem();
            }

            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
        }).WithSummary("The group's gathering point (members only; 204 when not set yet)");

        group.MapPost("/{groupId:guid}/attendance/confirm", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ConfirmArrivalCommand(groupId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Pilgrim confirms \"I arrived\" (idempotent; notifies the supervisor)");

        group.MapGet("/{groupId:guid}/attendance", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupAttendanceQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Roll call: pilgrims with status and counts (group supervisor only)");

        group.MapPut("/{groupId:guid}/attendance/{userId:guid}", async (Guid groupId, Guid userId, SetAttendanceRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetAttendanceCommand(groupId, userId, body.Status), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Mark a pilgrim present (1) or absent (2) manually (group supervisor only)");

        group.MapPost("/{groupId:guid}/attendance/reset", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ResetAttendanceCommand(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Start a new roll call: every pilgrim back to absent (group supervisor only)");

        group.MapGet("/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupMemberQuery(groupId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Member card incl. health notes (group supervisor only)");

        group.MapPut("/{groupId:guid}/members/{userId:guid}/notes", async (Guid groupId, Guid userId, SetMemberNotesRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetMemberNotesCommand(groupId, userId, body.Notes), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Supervisor's private notes about a member (null clears)");

        group.MapPost("/{groupId:guid}/messages", async (Guid groupId, SendChatMessageRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SendChatMessageCommand(groupId, body.Body), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/groups/{groupId}/messages", result.Value)
                : result.Error!.ToProblem();
        }).WithSummary("Send a message to the group chat (members only)");

        group.MapGet("/{groupId:guid}/messages", async (Guid groupId, ISender sender, CancellationToken ct, DateTimeOffset? before, int take = Paging.DefaultTake) =>
        {
            var result = await sender.Send(new GetChatMessagesQuery(groupId, before, take), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Group chat history, newest first (cursor: before = oldest sentAtUtc; members only)");

        return app;
    }
}