using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Journeys.Commands.CompleteJourneySetup;
using Labaik.Application.Features.Journeys.Commands.CreateJourney;
using Labaik.Application.Features.Journeys.Commands.SetArrivalPlan;
using Labaik.Application.Features.Journeys.Commands.SetStays;
using Labaik.Application.Features.Journeys.Queries.GetMyJourney;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/journeys")
            .WithApiVersionSet(versionSet)
            .WithTags("Journeys")
            .RequireAuthorization(); // all journey endpoints require a valid JWT

        group.MapPost("/", async (CreateJourneyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/journeys/{result.Value}", new { id = result.Value })
                : result.Error!.ToProblem();
        }).WithSummary("Create a new journey (starts the onboarding)");

        group.MapPut("/{id:guid}/arrival", async (Guid id, SetArrivalPlanBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetArrivalPlanCommand(id, body.Mode, body.EntryPoint), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Set how the pilgrim arrives and the entry point");

        group.MapPut("/{id:guid}/stays", async (Guid id, SetStaysBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetStaysCommand(id, body.Stays), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Set accommodations and arrival times");

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CompleteJourneySetupCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Finish setup and mark the journey ready");

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyJourneyQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Get the current user's journey");

        return app;
    }
}

// Request bodies (id comes from the route, not the body).
public sealed record SetArrivalPlanBody(Labaik.Domain.Journeys.Enums.TransportMode Mode, string EntryPoint);
public sealed record SetStaysBody(List<Labaik.Application.Features.Journeys.Commands.SetStays.StayInput> Stays);