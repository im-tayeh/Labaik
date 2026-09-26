using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Groups.Commands.CreateGroup;
using Labaik.Application.Features.Groups.Commands.JoinGroup;
using Labaik.Application.Features.Groups.Queries.GetGroupByCode;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/groups")
            .WithApiVersionSet(versionSet)
            .WithTags("Groups")
            .RequireAuthorization();

        group.MapPost("/", async (CreateGroupCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Create a group (creator becomes the leader)");

        group.MapGet("/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetGroupByCodeQuery(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Preview a group by its join code");

        group.MapPost("/{code}/join", async (string code, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new JoinGroupCommand(code), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Join a group by its code");

        return app;
    }
}