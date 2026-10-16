using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Guides.Queries.GetGuideArticleById;
using Labaik.Application.Features.Guides.Queries.GetGuideArticles;
using Labaik.Domain.Guides.Enums;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/guides")
            .WithApiVersionSet(versionSet)
            .WithTags("Guides")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct, GuideCategory category = GuideCategory.IhramPreparation) =>
        {
            var result = await sender.Send(new GetGuideArticlesQuery(category), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("List published guide articles of a category in order (default: Ihram preparation)");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGuideArticleByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Get one guide article");

        return app;
    }
}
