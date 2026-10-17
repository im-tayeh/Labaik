using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Companies.Queries.GetCompanyHome;
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
            .RequireAuthorization(AuthorizationPolicies.Company);

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyCompanyQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("The current account's company (welcome header)");

        group.MapGet("/home", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyHomeQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Company dashboard numbers: active trips, pilgrims, supervisors, groups");

        group.MapPost("/trips", async (CreateTripCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/trips/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        }).WithSummary("Create a trip for my company");

        group.MapGet("/trips", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("My company's trips, soonest departure first");

        group.MapGet("/trips/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyTripByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Trip details (my company's trips only)");

        group.MapPut("/trips/{id:guid}", async (Guid id, UpdateTripRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(UpdateTripCommand.From(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Edit a trip (all fields)");

        group.MapPost("/trips/{tripId:guid}/groups", async (Guid tripId, CreateTripGroupRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateTripGroupCommand(tripId, body.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/company/trips/{tripId}/groups", result.Value)
                : result.Error!.ToProblem();
        }).WithSummary("Create a group under one of my trips (starts without a supervisor)");

        group.MapGet("/trips/{tripId:guid}/groups", async (Guid tripId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTripGroupsQuery(tripId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("The groups under one of my trips");

        group.MapPost("/groups/{groupId:guid}/members", async (Guid groupId, AddGroupMemberRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddGroupMemberCommand(groupId, body.Email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Add a pilgrim (existing account, by email) to one of my groups");

        group.MapDelete("/groups/{groupId:guid}/members/{userId:guid}", async (Guid groupId, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveGroupMemberCommand(groupId, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Remove a pilgrim from one of my groups");

        group.MapPut("/groups/{groupId:guid}/supervisor", async (Guid groupId, AssignGroupSupervisorRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AssignGroupSupervisorCommand(groupId, body.Email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Assign (or replace) the group's supervisor by email; grants the Supervisor role");

        return app;
    }
}
