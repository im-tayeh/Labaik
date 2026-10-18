using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Announcements.Commands.PostCompanyAnnouncement;
using Labaik.Application.Features.Companies.Commands.AddCompanySupervisor;
using Labaik.Application.Features.Companies.Commands.UpdateMyCompany;
using Labaik.Application.Features.Companies.Queries.GetCompanyHome;
using Labaik.Application.Features.Companies.Queries.GetCompanySupervisors;
using Labaik.Application.Features.Companies.Queries.GetMyCompany;
using Labaik.Application.Features.Groups.Commands.AddGroupMember;
using Labaik.Application.Features.Groups.Commands.AssignGroupSupervisor;
using Labaik.Application.Features.Groups.Commands.CreateTripGroup;
using Labaik.Application.Features.Groups.Commands.RemoveGroupMember;
using Labaik.Application.Features.Groups.Queries.GetTripGroups;
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
        .WithSummary("The current account's company (welcome header + edit company data screen)")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Company);

        group.MapPut("/me", async (UpdateMyCompanyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Edit my company's data (name, logo, registration number, place, contacts; contract fields are admin-only)")
        .WithRequestExample(CompanyExamples.UpdateMyCompany)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Company)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/home", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyHomeQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Company dashboard numbers: active trips, pilgrims, roster supervisors, groups")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Home);

        group.MapPost("/supervisors", async (AddCompanySupervisorCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/supervisors/{result.Value.UserId}", result.Value)
                : result.Error!.ToProblem();
        })
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
        .WithSummary("My supervisor roster, by name, with how many of my groups each supervises")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Supervisors);

        group.MapPost("/announcements", async (PostCompanyAnnouncementCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/announcements/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
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
        .WithSummary("Create a trip for my company")
        .WithRequestExample(CompanyExamples.CreateTrip)
        .WithResponseExample(StatusCodes.Status201Created, CompanyExamples.Trip)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/trips", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("My company's trips, soonest departure first")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trips);

        group.MapGet("/trips/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Trip details (my company's trips only)")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trip)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/trips/{id:guid}", async (Guid id, UpdateTripRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(UpdateTripCommand.From(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Edit a trip (all fields)")
        .WithRequestExample(CompanyExamples.UpdateTrip)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Trip)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/trips/{tripId:guid}/groups", async (Guid tripId, CreateTripGroupRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateTripGroupCommand(tripId, body.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/trips/{tripId}/groups", result.Value)
                : result.Error!.ToProblem();
        })
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
        .WithSummary("The groups under one of my trips")
        .WithResponseExample(StatusCodes.Status200OK, new[] { CompanyExamples.Group })
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/groups/{groupId:guid}/members", async (Guid groupId, AddGroupMemberRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddGroupMemberCommand(groupId, body.Email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Add a pilgrim (existing account, by email) to one of my groups")
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Group)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/groups/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveGroupMemberCommand(groupId, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithSummary("Remove a pilgrim from one of my groups")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/groups/{groupId:guid}/supervisor", async (Guid groupId, AssignGroupSupervisorRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AssignGroupSupervisorCommand(groupId, body.Email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Assign (or replace) the group's supervisor by email; must be on my roster (else 404)")
        .WithRequestExample(CompanyExamples.AssignSupervisor)
        .WithResponseExample(StatusCodes.Status200OK, CompanyExamples.Group)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
