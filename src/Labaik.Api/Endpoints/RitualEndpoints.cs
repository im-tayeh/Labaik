using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Rituals.Commands.CompleteRitualStep;
using Labaik.Application.Features.Rituals.Commands.ResetRitualProgress;
using Labaik.Application.Features.Rituals.Queries.GetRitualProgress;
using Labaik.Application.Features.Rituals.Queries.GetRitualSteps;
using Labaik.Domain.Journeys.Enums;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class RitualEndpoints
{
    public static IEndpointRouteBuilder MapRitualEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/rituals")
            .WithApiVersionSet(versionSet)
            .WithTags("Rituals")
            .RequireAuthorization();

        group.MapGet("/steps", async (ISender sender, CancellationToken ct, PilgrimageType pilgrimageType = PilgrimageType.Umrah) =>
        {
            var result = await sender.Send(new GetRitualStepsQuery(pilgrimageType), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("List the published ritual guide steps in order");

        group.MapGet("/progress", async (ISender sender, CancellationToken ct, PilgrimageType pilgrimageType = PilgrimageType.Umrah) =>
        {
            var result = await sender.Send(new GetRitualProgressQuery(pilgrimageType), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Get the current user's ritual progress (starts it on first call)");

        group.MapPost("/progress/complete", async (CompleteRitualStepCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Complete the current ritual step and advance to the next");

        group.MapPost("/progress/reset", async (ResetRitualProgressCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Restart the ritual progress from the first step");

        return app;
    }
}
