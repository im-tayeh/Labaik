using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Companies.Queries.GetMyCompany;
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

        return app;
    }
}
