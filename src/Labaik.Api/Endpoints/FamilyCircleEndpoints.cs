using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.FamilyCircles.Commands.CreateFamilyCircle;
using Labaik.Application.Features.FamilyCircles.Commands.JoinFamilyCircle;
using Labaik.Application.Features.FamilyCircles.Commands.RemoveFamilyMember;
using Labaik.Application.Features.FamilyCircles.Commands.SetFamilyLocationSharing;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Application.Features.FamilyCircles.Queries.GetFamilyCircles;
using Labaik.Application.Features.FamilyCircles.Queries.GetFamilyLocations;
using MediatR;

namespace Labaik.Api.Endpoints;

// Family circle screens: independent of company groups. Live positions come from the
// one location pipeline (/location + /hubs/location); "start chat" has no backend yet (1:1 chat deferred).
public static class FamilyCircleEndpoints
{
    public static IEndpointRouteBuilder MapFamilyCircleEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/family-circles")
            .WithApiVersionSet(versionSet)
            .WithTags("Family circle")
            .RequireAuthorization();

        group.MapPost("/", async (CreateFamilyCircleCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/family-circles/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithSummary("Create a circle (I become its owner; share joinCode)")
        .Produces<FamilyCircleDto>(StatusCodes.Status201Created);

        group.MapGet("/mine", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyFamilyCirclesQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Circles I am in")
        .Produces<IReadOnlyList<FamilyCircleSummaryDto>>();

        group.MapGet("/preview/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFamilyCirclePreviewQuery(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Name, owner and size of the circle behind a join code (confirm screen)")
        .Produces<FamilyCirclePreviewDto>();

        group.MapPost("/join", async (JoinFamilyCircleCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Join a circle by code (the owner gets notification type 12)")
        .Produces<FamilyCircleDto>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFamilyCircleQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Circle details and members (members only)")
        .Produces<FamilyCircleDto>();

        group.MapGet("/{id:guid}/locations", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFamilyLocationsQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Members' live positions (only for members who show their location in this circle)")
        .Produces<IReadOnlyList<FamilyMemberLocationDto>>();

        group.MapPut("/{id:guid}/location-sharing", async (Guid id, SetFamilyLocationSharingRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetFamilyLocationSharingCommand(id, body.IsSharing), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Show/hide my location in this circle (global sharing via PUT /location/sharing must also be on)")
        .Produces<FamilyLocationSharingDto>();

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RemoveFamilyMemberCommand(id, userId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithSummary("Owner removes a member, or I leave (my own userId); the owner cannot leave")
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
