using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Announcements.Commands.MarkAnnouncementRead;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class AnnouncementEndpoints
{
    public static IEndpointRouteBuilder MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/announcements")
            .WithApiVersionSet(versionSet)
            .WithTags("Announcements")
            .RequireAuthorization();

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkAnnouncementReadCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("MarkAnnouncementRead")
        .WithSummary("Mark an announcement as read by the current user (idempotent)")
        .ReturnsNoContent().Fails(401, 404);

        return app;
    }
}
