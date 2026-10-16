using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Locations.Commands.SetLocationSharing;
using Labaik.Application.Features.Locations.Commands.UpdateMyLocation;
using Labaik.Application.Features.Locations.Queries.GetMyLocationSharing;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class LocationEndpoints
{
    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/location")
            .WithApiVersionSet(versionSet)
            .WithTags("Location")
            .RequireAuthorization();

        group.MapGet("/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyLocationSharingQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Get my location-sharing status and last position");

        group.MapPut("/sharing", async (SetLocationSharingCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Turn location sharing on or off (off erases the last position)");

        group.MapPost("/", async (UpdateMyLocationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithSummary("Send my current position (REST fallback for the realtime hub)");

        return app;
    }
}
