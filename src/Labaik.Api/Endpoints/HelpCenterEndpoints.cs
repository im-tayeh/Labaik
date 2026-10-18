using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.HelpCenter.Commands.MarkLostChildFound;
using Labaik.Application.Features.HelpCenter.Commands.ReportLostChild;
using Labaik.Application.Features.HelpCenter.Dtos;
using Labaik.Application.Features.HelpCenter.Queries.GetLostChildReports;
using Labaik.Application.Features.HelpCenter.Queries.GetMySupervisor;
using MediatR;

namespace Labaik.Api.Endpoints;

// Help center: contact my supervisor, lost-child reports.
public static class HelpCenterEndpoints
{
    public static IEndpointRouteBuilder MapHelpCenterEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var pilgrim = app
            .MapGroup("/api/v{version:apiVersion}/pilgrim")
            .WithApiVersionSet(versionSet)
            .WithTags("Help center")
            .RequireAuthorization();

        pilgrim.MapGet("/supervisor", async (Guid? groupId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMySupervisorQuery(groupId), ct);
            if (result.IsFailure)
            {
                return result.Error!.ToProblem();
            }

            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
        })
        .WithSummary("My supervisor's contact (groupId optional; 204 when I am in no supervised group)")
        .Produces<SupervisorContactDto>()
        .Produces(StatusCodes.Status204NoContent);

        var reports = app
            .MapGroup("/api/v{version:apiVersion}/lost-child-reports")
            .WithApiVersionSet(versionSet)
            .WithTags("Help center")
            .RequireAuthorization();

        reports.MapPost("/", async (ReportLostChildCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/lost-child-reports/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithSummary("Report a lost child (my supervisors are alerted; shareWithNearby also alerts users within 1 km)")
        .Produces<LostChildReportDto>(StatusCodes.Status201Created);

        reports.MapGet("/mine", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyLostChildReportsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("My lost-child reports, newest first")
        .Produces<IReadOnlyList<LostChildReportDto>>();

        reports.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetLostChildReportQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("One report (the reporter or a user it alerted via notification type 11)")
        .Produces<LostChildReportDto>();

        reports.MapPost("/{id:guid}/found", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkLostChildFoundCommand(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Close my report: the child was found (idempotent; reporter only)")
        .Produces<LostChildReportDto>();

        return app;
    }
}
