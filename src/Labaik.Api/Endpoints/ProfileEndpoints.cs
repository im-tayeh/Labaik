using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Profiles;
using Labaik.Application.Features.Profiles.Budget;
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
        })
        .WithName("GetMyProfile")
        .WithSummary("My profile (gender, nationality, photo URL, health notes)")
        .Returns<MyProfileDto>().Fails(401);

        group.MapPut("/profile", async (UpdateMyProfileCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateMyProfile")
        .WithSummary("Replace my profile (null clears a field)")
        .Returns<MyProfileDto>().Fails(400, 401).Validates();

        group.MapGet("/budget", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyBudgetQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetMyBudget")
        .WithSummary("Get my locally synchronized budget and expenses")
        .Returns<BudgetDto>().Fails(401);

        group.MapPut("/budget", async (ReplaceMyBudgetCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("ReplaceMyBudget")
        .WithSummary("Replace my complete budget and expense list")
        .Returns<BudgetDto>().Fails(400, 401).Validates();

        return app;
    }
}
