using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Groups.Queries.GetSupervisorGroups;
using MediatR;

namespace Labaik.Api.Endpoints;

// The supervisor view (Supervisor or Admin role).
public static class SupervisorEndpoints
{
    public static IEndpointRouteBuilder MapSupervisorEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/supervisor")
            .WithApiVersionSet(versionSet)
            .WithTags("Supervisor")
            .RequireAuthorization(AuthorizationPolicies.Supervisor);

        group.MapGet("/groups", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSupervisorGroupsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Groups I supervise, with pilgrim and attendance counts (soonest departure first)");

        return app;
    }
}
