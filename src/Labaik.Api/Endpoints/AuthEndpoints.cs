using Labaik.Api.Extensions;
using Labaik.Application.Features.Authentication.ForgotPassword;
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
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "A verification code has been sent to your email." })
                : result.Error!.ToProblem();
        })
        .WithSummary("Register a new account and send a verification code");

        group.MapPost("/verify-email", async (VerifyEmailCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Verify email with the OTP and receive tokens");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Authenticate and receive tokens");

        group.MapPost("/refresh-token", async (RefreshTokenCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        })
        .WithSummary("Exchange a refresh token for a new token pair");

        group.MapPost("/resend-code", async (ResendCodeCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "If the account exists and is unconfirmed, a new code has been sent." })
                : result.Error!.ToProblem();
        })
        .WithSummary("Resend the email verification code");

        group.MapPost("/forgot-password", async (ForgotPasswordCommand command, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(command, ct);
            return Results.Ok(new { message = "If the email exists, a reset code has been sent." });
        })
        .WithSummary("Send a password reset code");

        group.MapPost("/reset-password", async (ResetPasswordCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess
                ? Results.Ok(new { message = "Password changed. You can now log in." })
                : result.Error!.ToProblem();
        })
        .WithSummary("Reset the password using the emailed code");

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            email = user.FindFirstValue(JwtRegisteredClaimNames.Email),
            name = user.FindFirstValue(JwtRegisteredClaimNames.Name)
        }))
        .RequireAuthorization()
        .WithSummary("Return the authenticated user's claims");

        return app;
    }
}