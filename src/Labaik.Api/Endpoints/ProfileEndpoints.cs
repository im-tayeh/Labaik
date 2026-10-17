using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Profiles;
using MediatR;

namespace Labaik.Api.Endpoints;

// The signed-in user's own profile (health notes are visible only to them and their group supervisors).
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/me")
            .WithApiVersionSet(versionSet)
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/profile", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyProfileQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("My profile (gender, nationality, photo URL, health notes)");

        group.MapPut("/profile", async (UpdateMyProfileCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Replace my profile (null clears a field)");

        return app;
    }
}
