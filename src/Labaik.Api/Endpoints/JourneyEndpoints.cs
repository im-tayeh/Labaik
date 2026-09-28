using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Journeys.Commands.CreateJourney;
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
                ? Results.Created($"/api/v1/journeys/{result.Value}", new { id = result.Value })
                : result.Error!.ToProblem();
        }).WithSummary("Create a complete journey from onboarding");

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyJourneyQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Get the current user's journey");

        return app;
    }
}