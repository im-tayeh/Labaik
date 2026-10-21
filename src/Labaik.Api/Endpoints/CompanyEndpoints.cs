using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Announcements.Commands.PostCompanyAnnouncement;
using Labaik.Application.Features.Companies.Commands.AddCompanySupervisor;
using Labaik.Application.Features.Companies.Commands.IssueSupervisorTempPassword;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Commands.UpdateMyCompany;
using Labaik.Application.Features.Companies.Queries.GetCompanyHome;
using Labaik.Application.Features.Companies.Queries.GetCompanySupervisors;
using Labaik.Application.Features.Companies.Queries.GetCompanyPilgrims;
using Labaik.Application.Features.Companies.Queries.GetMyCompany;
using Labaik.Application.Features.Groups.Commands.AddGroupMember;
using Labaik.Application.Features.Groups.Commands.AssignGroupSupervisor;
using Labaik.Application.Features.Groups.Commands.CreateTripGroup;
using Labaik.Application.Features.Groups.Commands.UpdateCompanyGroup;
using Labaik.Application.Features.Groups.Commands.DeleteCompanyGroup;
using Labaik.Application.Features.Groups.Commands.RemoveGroupMember;
using Labaik.Application.Features.Groups.Queries.GetGroupMember;
using Labaik.Application.Features.Groups.Queries.GetGroupMembers;
using Labaik.Application.Features.Groups.Queries.GetTripGroups;
using Labaik.Application.Features.Groups.Queries.GetCompanyGroups;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Application.Features.Trips.Commands.CreateTrip;
using Labaik.Application.Features.Trips.Commands.UpdateTrip;
using Labaik.Application.Features.Trips.Queries.GetCompanyTripById;
using Labaik.Application.Features.Trips.Queries.GetCompanyTrips;
using MediatR;

namespace Labaik.Api.Endpoints;

// The company view (company owner accounts only).
public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/company")
            .WithApiVersionSet(versionSet)
            .WithTags("Company")
            .RequireAuthorization(AuthorizationPolicies.Company)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyCompanyQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetMyCompany")
        .WithSummary("The current account's company (welcome header + edit company data screen)")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Company);

        group.MapPut("/me", async (UpdateMyCompanyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateMyCompany")
        .WithSummary("Edit my company's data (name, logo, registration number, place, contacts; contract fields are admin-only)")
        .WithRequestExample(CompanyExamples.UpdateMyCompany)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Company)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/supervisors/{userId:guid}/temp-password", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new IssueSupervisorTempPasswordCommand(userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("IssueSupervisorTemporaryPassword")
        .WithSummary("Issue and email a temporary password; returns it only for accounts provisioned by this company")
        .Returns<TemporaryPasswordDto>().Fails(404);

        group.MapGet("/home", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyHomeQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).RequireCompanyService(s => s.ReportsAndStats)
        .WithName("GetCompanyHome")
        .WithSummary("Company dashboard numbers: active trips, pilgrims, roster supervisors, groups")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Home);

        group.MapPost("/supervisors", async (AddCompanySupervisorCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/supervisors/{result.Value.UserId}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("AddCompanySupervisor")
        .WithSummary("Create a supervisor account on my roster (new email: temporary password emailed; registered email: linked)")
        .WithRequestExample(CompanyExamples.AddSupervisor)
        .WithResponseExample(StatusCodes.Status201Created, CompanyExamples.Supervisor with { GroupsCount = 0 })
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/supervisors", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanySupervisorsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanySupervisors")
        .WithSummary("My supervisor roster, by name, with how many of my groups each supervises")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Supervisors);

        group.MapGet("/groups", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyGroupsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyGroups")
        .WithSummary("All my groups across trips, soonest trip first")
        .Returns<IReadOnlyList<CompanyGroupListItemDto>>().Fails(401);

        group.MapPut("/groups/{groupId:guid}", async (Guid groupId, UpdateCompanyGroupRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCompanyGroupCommand(groupId, body.Name, body.Notes, body.Capacity), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateCompanyGroup")
        .WithSummary("Rename a group and set its notes and capacity")
        .Returns<CompanyGroupDto>().Fails(400, 401, 403, 404, 409).Validates();

        group.MapDelete("/groups/{groupId:guid}", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteCompanyGroupCommand(groupId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("DeleteCompanyGroup")
        .WithSummary("Delete a group with no pilgrims")
        .ReturnsNoContent().Fails(401, 403, 404, 409);

        group.MapGet("/pilgrims", async (ISender sender, CancellationToken ct, string? search, int take = 100) =>
        {
            var result = await sender.Send(new GetCompanyPilgrimsQuery(search, take), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyPilgrims")
        .WithSummary("Every pilgrim in my groups (distinct), with the groups they are in; search by name or email")
        .Returns<IReadOnlyList<CompanyPilgrimDto>>().Fails(401, 403).Validates();

        group.MapPost("/announcements", async (PostCompanyAnnouncementCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/announcements/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("PostCompanyAnnouncement")
        .WithSummary("Announce to every pilgrim in all my company's groups (audience 2)")
        .WithRequestExample(CompanyExamples.Announce)
        .WithResponseExample(StatusCodes.Status201Created, CompanyExamples.Announcement)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/trips", async (CreateTripCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/trips/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("CreateTrip")
        .WithSummary("Create a trip for my company")
        .WithRequestExample(CompanyExamples.CreateTrip)
        .WithResponseExample(StatusCodes.Status201Created, CompanyExamples.Trip)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/trips", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyTrips")
        .WithSummary("My company's trips, soonest departure first")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trips);

        group.MapGet("/trips/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyTrip")
        .WithSummary("Trip details (my company's trips only)")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trip)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/trips/{id:guid}", async (Guid id, UpdateTripRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(UpdateTripCommand.From(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateTrip")
        .WithSummary("Edit a trip (all fields)")
        .WithRequestExample(CompanyExamples.UpdateTrip)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trip)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/trips/{tripId:guid}/groups", async (Guid tripId, CreateTripGroupRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateTripGroupCommand(tripId, body.Name, body.Notes, body.Capacity), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/trips/{tripId}/groups", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("CreateTripGroup")
        .WithSummary("Create a group under one of my trips (starts without a supervisor)")
        .WithRequestExample(CompanyExamples.CreateGroup)
        .WithResponseExample(StatusCodes.Status201Created, CompanyExamples.NewGroup)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/trips/{tripId:guid}/groups", async (Guid tripId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTripGroupsQuery(tripId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetTripGroups")
        .WithSummary("The groups under one of my trips")
        .WithResponseExample(StatusCodes.Status200OK, new[] { CompanyExamples.Group })
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{groupId:guid}/members", async (Guid groupId, AddGroupMemberRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddGroupMemberCommand(groupId, body.Email, body.FullName, body.PhoneNumber), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("AddGroupMember")
        .WithSummary("Add a pilgrim by email to one of my groups (unknown email: pilgrim account created, temporary password emailed)")
        .WithRequestExample(CompanyExamples.AddMember)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Group)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/groups/{groupId:guid}/members", async (Guid groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyGroupMembersQuery(groupId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyGroupMembers")
        .WithSummary("Members of one of my groups (supervisor first, then pilgrims by number; no health notes)")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.MemberRows)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/groups/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyGroupMemberQuery(groupId, userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanyGroupMember")
        .WithSummary("Member card incl. health notes (sensitive) for one of my groups")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.MemberCard)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/groups/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveGroupMemberCommand(groupId, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("RemoveGroupMember")
        .WithSummary("Remove a pilgrim from one of my groups")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/groups/{groupId:guid}/supervisor", async (Guid groupId, AssignGroupSupervisorRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AssignGroupSupervisorCommand(groupId, body.Email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("AssignGroupSupervisor")
        .WithSummary("Assign (or replace) the group's supervisor by email; must be on my roster (else 404)")
        .WithRequestExample(CompanyExamples.AssignSupervisor)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Group)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
