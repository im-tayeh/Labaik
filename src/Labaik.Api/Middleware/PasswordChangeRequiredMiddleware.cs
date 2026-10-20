using Microsoft.AspNetCore.Mvc;

namespace Labaik.Api.Middleware;

// While the token carries pwd_change=true, only the endpoints needed to change the password work.
internal sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedSuffixes =
        ["/auth/change-password", "/auth/logout", "/auth/me", "/auth/refresh-token"];

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChange = context.User.HasClaim("pwd_change", "true");
        var path = context.Request.Path.Value ?? string.Empty;

        if (mustChange && !AllowedSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            await Results.Problem(
                title: "You must change your password before continuing.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "Auth.PasswordChangeRequired" })
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
