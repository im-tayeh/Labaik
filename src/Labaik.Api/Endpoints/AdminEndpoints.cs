using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Admin.Commands.CreateCompany;
using Labaik.Application.Features.Admin.Commands.SetCompanyStopped;
using Labaik.Application.Features.Admin.Commands.SetSupervisorRole;
using Labaik.Application.Features.Admin.Commands.UpdateCompany;
using Labaik.Application.Features.Admin.Queries.FindUserByEmail;
using Labaik.Application.Features.Admin.Queries.GetAdminOverview;
using Labaik.Application.Features.Admin.Queries.GetCompanies;
using Labaik.Application.Features.Admin.Queries.GetCompanyById;
using Labaik.Domain.Companies.Enums;
using MediatR;

namespace Labaik.Api.Endpoints;

// Admin dashboard (separate web client): platform operators only.
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();

        var group = app
            .MapGroup("/api/v{version:apiVersion}/admin")
            .WithApiVersionSet(versionSet)
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/overview", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAdminOverviewQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Dashboard home numbers: subscriptions, expiring soon, active pilgrims, companies")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Overview);

        group.MapGet("/users", async (string email, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new FindUserByEmailQuery(email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Find an account by email (admin)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.User)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: true), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Grant the Supervisor role (admin; applies from the user's next token)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.User)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: false), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Revoke the Supervisor role (admin; applies from the user's next token)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.User with { Roles = [] })
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/companies", async (CreateCompanyCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/admin/companies/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        })
        .WithSummary("Create a company and its owner account (new email: temporary password emailed; registered email: linked as owner)")
        .WithRequestExample(AdminExamples.CreateCompany)
        .WithResponseExample(StatusCodes.Status201Created, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/companies", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompaniesQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Companies table (newest first) with active pilgrims, subscription dates, status and days remaining")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Companies);

        group.MapGet("/companies/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Company details: every form field, owner, status and live numbers")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/companies/{id:guid}", async (Guid id, UpdateCompanyRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCompanyCommand(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Edit a company (full replace of the form; the owner account is not changed)")
        .WithRequestExample(AdminExamples.UpdateCompany)
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/companies/{id:guid}/stop", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetCompanyStoppedCommand(id, IsStopped: true), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Suspend a company (status 2 Stopped; idempotent; nothing else is enforced yet)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company with
        {
            IsStopped = true,
            Status = CompanyStatus.Stopped
        })
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/companies/{id:guid}/resume", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetCompanyStoppedCommand(id, IsStopped: false), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Resume a stopped company (status follows the contract dates again; idempotent)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
