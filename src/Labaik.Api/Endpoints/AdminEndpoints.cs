using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Application.Features.Admin.Commands.CreateCompany;
using Labaik.Application.Features.Admin.Commands.SetSupervisorRole;
using Labaik.Application.Features.Admin.Queries.FindUserByEmail;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/admin")
            .WithApiVersionSet(versionSet)
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapGet("/users", async (string email, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new FindUserByEmailQuery(email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Find an account by email (admin)");

        group.MapPut("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: true), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Grant the Supervisor role (admin; applies from the user's next token)");

        group.MapDelete("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: false), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithSummary("Revoke the Supervisor role (admin; applies from the user's next token)");

        group.MapPost("/companies", async (CreateCompanyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/admin/companies/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        }).WithSummary("Create a company and its owner account (new email: temporary password emailed; registered email: linked as owner)");

        return app;
    }
}
