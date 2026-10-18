using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Journeys.Dtos;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Journeys.Commands.CreateJourney;
using Labaik.Application.Features.Journeys.Commands.UpdateJourney;
using Labaik.Application.Features.Journeys.Queries.GetJourneyById;
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
            .RequireAuthorization();

        group.MapPost("/", async (CreateJourneyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/journeys/{result.Value}", new CreatedIdResponse(result.Value))
                : result.Error!.ToProblem();
        })
        .WithName("CreateJourney")
        .WithSummary("Create a complete journey from onboarding")
        .Returns<CreatedIdResponse>(StatusCodes.Status201Created).Fails(400, 401).Validates();

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyJourneyQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetMyJourney")
        .WithSummary("Get the current user's journey")
        .Returns<JourneyDto>().Fails(401, 404);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetJourneyByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetJourneyById")
        .WithSummary("Get trip details of one of the current user's journeys")
        .Returns<JourneyDto>().Fails(401, 404);

        group.MapPut("/{id:guid}", async (Guid id, UpdateJourneyRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(UpdateJourneyCommand.From(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateJourney")
        .WithSummary("Edit trip details (allowed while the journey is Draft or Ready)")
        .Returns<JourneyDto>().Fails(400, 401, 404, 409).Validates();

        return app;
    }
}