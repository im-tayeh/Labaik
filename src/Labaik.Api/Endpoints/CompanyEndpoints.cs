using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Companies.Queries.GetMyCompany;
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

        return app;
    }
}
