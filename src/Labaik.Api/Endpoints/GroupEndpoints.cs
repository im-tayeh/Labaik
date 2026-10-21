using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Announcements.Commands.PostAnnouncement;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Application.Features.Announcements.Queries.GetGroupAnnouncements;
using Labaik.Application.Features.Chat.Commands.MarkChatRead;
using Labaik.Application.Features.Chat.Commands.SendChatMessage;
using Labaik.Application.Features.Chat.Commands;
using Labaik.Application.Features.Chat.Dtos;
using Labaik.Application.Features.Chat.Queries.GetChatMessages;
using Labaik.Application.Features.Chat.Queries.GetChatUnreadCount;
using Labaik.Application.Features.Groups.Commands.Attendance;
using Labaik.Application.Features.Groups.Commands.CreateGroup;
using Labaik.Application.Features.Groups.Commands.JoinGroup;
using Labaik.Application.Features.Groups.Commands.SetGatheringPoint;
using Labaik.Application.Features.Groups.Commands.SetMemberNotes;
using Labaik.Application.Features.Groups.Commands.AddGroupMember;
using Labaik.Application.Features.Groups.Commands.SupervisorAddMember;
using Labaik.Application.Features.Groups.Commands.SupervisorRemoveMember;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Application.Features.Groups.Queries.GetGatheringPoint;
using Labaik.Application.Features.Groups.Queries.GetGroupAttendance;
using Labaik.Application.Features.Groups.Queries.GetGroupByCode;
using Labaik.Application.Features.Groups.Queries.GetGroupMember;
using Labaik.Application.Features.Groups.Queries.GetGroupMembers;
using Labaik.Application.Features.Locations.Dtos;
using Labaik.Application.Features.Locations.Queries.GetGroupMemberLocations;
using Labaik.Application.Features.Notifications.Dtos;
using Labaik.Domain.Chat;
using Labaik.Domain.Companies;
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
        .WithName("CreateGroup")
        .WithSummary("Create a standalone group (admin only; companies use /company/trips/{tripId}/groups)")
        .Returns<GroupDto>().Fails(401, 403).Validates();

        group.MapGet("/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupByCodeQuery(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetGroupByCode")
        .WithSummary("Preview a group by its join code")
        .Returns<GroupSummaryDto>().Fails(401, 404);

        group.MapPost("/{code}/join", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new JoinGroupCommand(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("JoinGroup")
        .WithSummary("Join a group by its code (any signed-in user; 409 Group.AlreadyMember)")
        .Returns<GroupSummaryDto>().Fails(401, 403, 404, 409);

        group.MapPost("/{groupId:guid}/announcements", async (Guid groupId, PostAnnouncementRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(PostAnnouncementCommand.From(groupId, body), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/groups/{groupId}/announcements", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("PostGroupAnnouncement")
        .WithSummary("Broadcast an announcement to the group (leader only)")
        .Returns<AnnouncementDto>(StatusCodes.Status201Created).Fails(400, 401, 403, 404).Validates();

        group.MapGet("/{groupId:guid}/announcements", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupAnnouncementsQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetGroupAnnouncements")
        .WithSummary("List the group's announcements, newest first, with read state (members only)")
        .Returns<IReadOnlyList<AnnouncementDto>>().Fails(401, 404);

        group.MapGet("/{groupId:guid}/locations", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupMemberLocationsQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.GpsTracking)
        .WithName("GetGroupMemberLocations")
        .WithSummary("Where are my group members: every member with their shared position (members only)")
        .Returns<IReadOnlyList<MemberLocationDto>>().Fails(401, 404);

        group.MapPut("/{groupId:guid}/gathering-point", async (Guid groupId, SetGatheringPointRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(SetGatheringPointCommand.From(groupId, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("SetGatheringPoint")
        .WithSummary("Set or move the group's gathering point (group supervisor only; members are notified)")
        .Returns<GatheringPointDto>().Fails(400, 401, 403, 404).Validates();

        group.MapGet("/{groupId:guid}/gathering-point", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGatheringPointQuery(groupId), ct);
            if (result.IsFailure)
            {
                return result.Error!.ToProblem();
            }

            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
        })
        .WithName("GetGatheringPoint")
        .WithSummary("The group's gathering point (members only; 204 when not set yet)")
        .Returns<GatheringPointDto>().ReturnsNoContent().Fails(401, 404);

        group.MapPost("/{groupId:guid}/attendance/confirm", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ConfirmArrivalCommand(groupId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("ConfirmArrival")
        .WithSummary("Pilgrim confirms \"I arrived\" (idempotent; notifies the supervisor)")
        .ReturnsNoContent().Fails(401, 403, 404);

        group.MapGet("/{groupId:guid}/attendance", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupAttendanceQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetGroupAttendance")
        .WithSummary("Roll call: pilgrims with status and counts (group supervisor only)")
        .Returns<GroupAttendanceDto>().Fails(401, 403, 404);

        group.MapPut("/{groupId:guid}/attendance/{userId:guid}", async (Guid groupId, Guid userId, SetAttendanceRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetAttendanceCommand(groupId, userId, body.Status), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("SetAttendance")
        .WithSummary("Mark a pilgrim present (1) or absent (2) manually (group supervisor only)")
        .Returns<GroupAttendanceDto>().Fails(400, 401, 403, 404).Validates();

        group.MapPost("/{groupId:guid}/attendance/reset", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ResetAttendanceCommand(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("ResetAttendance")
        .WithSummary("Start a new roll call: every pilgrim back to absent (group supervisor only)")
        .Returns<GroupAttendanceDto>().Fails(401, 403, 404);

        group.MapGet("/{groupId:guid}/members", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupMembersQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetGroupMembers")
        .WithSummary("Member list: supervisor first, then pilgrims by member number; no health notes (group supervisor only)")
        .Returns<IReadOnlyList<GroupMemberRowDto>>().Fails(401, 403, 404);

        group.MapGet("/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupMemberQuery(groupId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetGroupMember")
        .WithSummary("Member card incl. health notes (group supervisor only)")
        .Returns<GroupMemberDetailsDto>().Fails(401, 403, 404);

        group.MapPost("/{groupId:guid}/members", async (Guid groupId, AddGroupMemberRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupervisorAddMemberCommand(groupId, body.Email, body.FullName, body.PhoneNumber), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("SupervisorAddGroupMember")
        .WithSummary("Add a pilgrim by email to my group (group supervisor only; unknown email gets a temporary-password email)")
        .Returns<Labaik.Application.Features.Groups.Dtos.CompanyGroupDto>().Fails(400, 401, 403, 404, 409).Validates();

        group.MapDelete("/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SupervisorRemoveMemberCommand(groupId, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("SupervisorRemoveGroupMember")
        .WithSummary("Remove a pilgrim from my group (group supervisor only; notifies the company)")
        .ReturnsNoContent().Fails(401, 403, 404);

        group.MapPut("/{groupId:guid}/members/{userId:guid}/notes", async (Guid groupId, Guid userId, SetMemberNotesRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetMemberNotesCommand(groupId, userId, body.Notes), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("SetMemberNotes")
        .WithSummary("Supervisor's private notes about a member (null clears)")
        .ReturnsNoContent().Fails(400, 401, 403, 404).Validates();

        group.MapPost("/{groupId:guid}/messages", async (Guid groupId, SendChatMessageRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SendChatMessageCommand(
                groupId, body.Body, body.Kind ?? ChatMessageKind.Text, body.Latitude, body.Longitude), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/groups/{groupId}/messages", result.Value)
                : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("SendChatMessage")
        .WithSummary("Send a text (kind 1) or location (kind 2: latitude/longitude, body = caption) message to the group chat (members only)")
        .Returns<ChatMessageDto>(StatusCodes.Status201Created).Fails(400, 401, 404).Validates();

        group.MapGet("/{groupId:guid}/messages", async (Guid groupId, ISender sender, CancellationToken ct, DateTimeOffset? before, int take = Paging.DefaultTake) =>
        {
            var result = await sender.Send(new GetChatMessagesQuery(groupId, before, take), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("GetChatMessages")
        .WithSummary("Group chat history for members or the owning company (read only), newest first")
        .Returns<IReadOnlyList<ChatMessageDto>>().Fails(401, 404).Validates();

        group.MapPost("/{groupId:guid}/messages/read", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkChatReadCommand(groupId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("MarkChatRead")
        .WithSummary("Mark the group chat read up to now (idempotent; members only)")
        .ReturnsNoContent().Fails(401, 404);

        group.MapGet("/{groupId:guid}/messages/unread-count", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetChatUnreadCountQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("GetChatUnreadCount")
        .WithSummary("Unread group chat messages from others (members only)")
        .Returns<UnreadCountDto>().Fails(401, 404);

        group.MapDelete("/{groupId:guid}/messages/{messageId:guid}", async (Guid groupId, Guid messageId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteChatMessageCommand(groupId, messageId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("DeleteChatMessage").WithSummary("Delete a chat message (author or supervisor)")
        .ReturnsNoContent().Fails(401, 403, 404);

        group.MapPost("/{groupId:guid}/chat/mute", async (Guid groupId, MuteChatMemberRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MuteChatMemberCommand(groupId, body.UserId, body.Minutes), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("MuteChatMember").WithSummary("Mute a group pilgrim for up to seven days (0 unmutes)")
        .ReturnsNoContent().Fails(401, 403, 404).Validates();

        group.MapPut("/{groupId:guid}/chat/settings", async (Guid groupId, SetChatSettingsRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetChatSettingsCommand(groupId, body.OnlySupervisorCanPost), ct);
            return result.IsSuccess ? Results.Ok(new { body.OnlySupervisorCanPost }) : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ChatWithGuides)
        .WithName("SetChatSettings").WithSummary("Set whether only the supervisor can post to the group chat")
        .Returns<object>().Fails(401, 403, 404);

        return app;
    }
}
