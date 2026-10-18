using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Trips.Itinerary;
using MediatR;

namespace Labaik.Api.Endpoints;

// Trip data shared by everyone on the trip (company, supervisors, pilgrims); access is checked per trip.
public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/trips")
            .WithApiVersionSet(versionSet)
            .WithTags("Trips")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/itinerary", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTripItineraryQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetTripItinerary")
        .WithSummary("Trip program by day (the company, and supervisors and pilgrims of the trip)")
        .Returns<TripItineraryDto>().Fails(401, 404);

        group.MapPut("/{id:guid}/itinerary", async (Guid id, ReplaceTripItineraryRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ReplaceTripItineraryCommand(id, body.Days), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("ReplaceTripItinerary")
        .WithSummary("Replace the whole trip program (the company or a supervisor of the trip)")
        .Returns<TripItineraryDto>().Fails(400, 401, 403, 404).Validates();

        return app;
    }
}
