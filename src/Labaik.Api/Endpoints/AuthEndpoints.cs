using Asp.Versioning;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Common.Models;
using Labaik.Application.Features.Authentication.ForgotPassword;
using Labaik.Application.Features.Authentication.GoogleSignIn;
using Labaik.Application.Features.Authentication.Login;
using Labaik.Application.Features.Authentication.RefreshToken;
using Labaik.Application.Features.Authentication.Register;
using Labaik.Application.Features.Authentication.ResendCode;
using Labaik.Application.Features.Authentication.ResetPassword;
using Labaik.Application.Features.Authentication.VerifyEmail;
using MediatR;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace Labaik.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiversion}/auth")
            .WithApiVersionSet(versionSet)
            .WithTags("Authentication");

        group.MapPost("/register", async (RegisterCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new MessageResponse("A verification code has been sent to your email."))
                : result.Error!.ToProblem();
        })
        .WithName("Register")
        .WithSummary("Register a new account and send a verification code")
        .Returns<MessageResponse>().Fails(400, 409, 429).Validates();

        group.MapPost("/verify-email", async (VerifyEmailCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("VerifyEmail")
        .WithSummary("Verify email with the OTP and receive tokens")
        .Returns<AuthTokens>().Fails(400, 404).Validates();

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("Login")
        .WithSummary("Authenticate and receive tokens (unconfirmed email: 403 code Auth.EmailNotConfirmed)")
        .Returns<AuthTokens>().Fails(401, 403).Validates();

        group.MapPost("/google", async (GoogleSignInCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("GoogleSignIn")
        .WithSummary("Sign in with a Google ID token (links by verified email or creates the account)")
        .Returns<AuthTokens>().Fails(400, 401).Validates();

        group.MapPost("/refresh-token", async (RefreshTokenCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token for a new token pair (single use; the old one is revoked)")
        .Returns<AuthTokens>().Fails(401).Validates();

        group.MapPost("/resend-code", async (ResendCodeCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new MessageResponse("If the account exists and is unconfirmed, a new code has been sent."))
                : result.Error!.ToProblem();
        })
        .WithName("ResendCode")
        .WithSummary("Resend the email verification code (429 within 55 s of the last one)")
        .Returns<MessageResponse>().Fails(429).Validates();

        group.MapPost("/forgot-password", async (ForgotPasswordCommand command, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(command, ct);
            return Results.Ok(new MessageResponse("If the email exists, a reset code has been sent."));
        })
        .WithName("ForgotPassword")
        .WithSummary("Send a password reset code (always 200: no account enumeration)")
        .Returns<MessageResponse>().Validates();

        group.MapPost("/reset-password", async (ResetPasswordCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new MessageResponse("Password changed. You can now log in."))
                : result.Error!.ToProblem();
        })
        .WithName("ResetPassword")
        .WithSummary("Reset the password using the emailed code (revokes all refresh tokens)")
        .Returns<MessageResponse>().Fails(400).Validates();

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            user.FindFirstValue(JwtRegisteredClaimNames.Email),
            user.FindFirstValue(JwtRegisteredClaimNames.Name),
            user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList())))
        .RequireAuthorization()
        .WithName("GetMe")
        .WithSummary("Return the authenticated user's claims (roles: [] for a pilgrim)")
        .Returns<MeResponse>().Fails(401);

        return app;
    }
}
