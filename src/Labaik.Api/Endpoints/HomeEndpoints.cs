using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Home.Queries.GetHome;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/home")
            .WithApiVersionSet(versionSet)
            .WithTags("Home")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetHomeQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Home screen: journey status, next ritual step, and group summary");

        return app;
    }
}
