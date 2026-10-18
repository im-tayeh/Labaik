using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Locations.Commands.UpdateMyLocation;
using Labaik.Application.Features.Map.Commands.ReportEmergency;
using Labaik.Application.Features.Map.Commands.ResolveEmergency;
using Labaik.Application.Features.Map.Dtos;
using Labaik.Application.Features.Map.Queries.GetEmergencyAlert;
using Labaik.Application.Features.Map.Queries.GetMapPlaces;
using Labaik.Application.Features.Map.Queries.GetRoute;
using Labaik.Domain.Map;
using MediatR;

namespace Labaik.Api.Endpoints;

// Field mode. The map itself is drawn by the client; the backend serves places, guidance and SOS.
public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/map")
            .WithApiVersionSet(versionSet)
            .WithTags("Map")
            .RequireAuthorization();

        group.MapGet("/destinations", async (MapPlaceType? type, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMapPlacesQuery(type), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetMapDestinations")
        .WithSummary("Haram places: type 1 main gates, 2 sub-gates, 3 facilities (omit type for all)")
        .Fails(401).Validates()
        .Produces<IReadOnlyList<MapPlaceDto>>();

        group.MapGet("/route", async (double fromLat, double fromLng, Guid? destinationId, double? toLat, double? toLng,
            ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRouteQuery(fromLat, fromLng, destinationId, toLat, toLng), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetRoute")
        .WithSummary("Straight-line guidance to a place (destinationId) or a point (toLat/toLng): distance, bearing, walking minutes")
        .Fails(401, 404).Validates()
        .Produces<MapRouteDto>();

        group.MapPost("/emergency", async (ReportEmergencyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/map/emergency/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithName("ReportEmergency")
        .WithSummary("SOS with my position; the supervisors of my groups get notification type 10")
        .Fails(400, 401).Validates()
        .Produces<EmergencyAlertDto>(StatusCodes.Status201Created);

        group.MapGet("/emergency/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEmergencyAlertQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetEmergency")
        .WithSummary("An SOS (the reporter or one of their supervisors)")
        .Fails(401, 404)
        .Produces<EmergencyAlertDto>();

        group.MapPost("/emergency/{id:guid}/resolve", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ResolveEmergencyCommand(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("ResolveEmergency")
        .WithSummary("Mark an SOS handled (idempotent; the reporter or one of their supervisors)")
        .Fails(401, 404)
        .Produces<EmergencyAlertDto>();

        // Same command as POST /location: one location pipeline (sharing rules, realtime broadcast).
        group.MapPost("/location", async (UpdateMyLocationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("UpdateMyLocationAlias")
        .WithSummary("Alias of POST /location (send my current position; sharing must be on)")
        .Fails(400, 401, 409).Validates()
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
