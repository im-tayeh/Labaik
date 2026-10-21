using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.Admin.Commands.CreateCompany;
using Labaik.Application.Features.Admin.Commands.DeleteCompany;
using Labaik.Application.Features.Admin.Commands.SetUserDisabled;
using Labaik.Application.Features.Admin.Commands.IssueCompanyTempPassword;
using Labaik.Application.Common.Provisioning;
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
        .WithName("GetAdminOverview")
        .WithSummary("Dashboard home numbers; pendingRequests is deprecated and always 0")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Overview);

        group.MapGet("/users", async (string email, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new FindUserByEmailQuery(email), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("FindUserByEmail")
        .WithSummary("Find an account by email (admin)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.User)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: true), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GrantSupervisorRole")
        .WithSummary("Grant the Supervisor role (admin; applies from the user's next token)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.User)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/users/{userId:guid}/supervisor", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetSupervisorRoleCommand(userId, IsSupervisor: false), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("RevokeSupervisorRole")
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
        .WithName("CreateCompany")
        .WithSummary("Create a company and its owner account (optional temporary password/force-change/email fields; password returned once for new accounts)")
        .WithRequestExample(AdminExamples.CreateCompany)
        .WithResponseExample(StatusCodes.Status201Created, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/companies/{id:guid}", async (Guid id, bool? force, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteCompanyCommand(id, force == true), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("DeleteCompany")
        .WithSummary("Soft-delete a company and disable its owner and roster supervisors; force may be needed for active trips")
        .ReturnsNoContent().Fails(404, 409);

        group.MapPost("/users/{userId:guid}/disable", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetUserDisabledCommand(userId, true), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("DisableUser")
        .WithSummary("Disable an account and revoke its refresh tokens")
        .ReturnsNoContent().Fails(404);

        group.MapPost("/users/{userId:guid}/enable", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SetUserDisabledCommand(userId, false), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        })
        .WithName("EnableUser")
        .WithSummary("Enable an account")
        .ReturnsNoContent().Fails(404);

        group.MapPost("/companies/{id:guid}/temp-password", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new IssueCompanyTempPasswordCommand(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("IssueCompanyTemporaryPassword")
        .WithSummary("Issue and email a new temporary password to the company owner; returns the password once")
        .Returns<TemporaryPasswordDto>().Fails(404);

        group.MapGet("/companies", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompaniesQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompanies")
        .WithSummary("Companies table (newest first) with active pilgrims, subscription dates, status and days remaining")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Companies);

        group.MapGet("/companies/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCompanyByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GetCompany")
        .WithSummary("Company details: every form field, owner, status and live numbers")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/companies/{id:guid}", async (Guid id, UpdateCompanyRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateCompanyCommand(id, body), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("UpdateCompany")
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
        .WithName("StopCompany")
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
        .WithName("ResumeCompany")
        .WithSummary("Resume a stopped company (status follows the contract dates again; idempotent)")
        .WithResponseExample(StatusCodes.Status200OK, AdminExamples.Company)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
