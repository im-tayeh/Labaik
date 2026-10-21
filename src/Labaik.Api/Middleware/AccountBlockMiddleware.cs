using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Labaik.Api.Extensions;
using Labaik.Api.Services;
using Labaik.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Labaik.Api.Middleware;

// Login and refresh refuse a stopped/expired company, a suspended supervisor and a disabled account, but an
// access token already issued stays valid for its lifetime. This re-runs the same checks on every
// authenticated request (cached a few seconds per user, dropped by IAccountAccessCache.Reset on every
// change), so the block takes effect on the next request.
// Logout stays open so the app can still sign out cleanly.
internal sealed class AccountBlockMiddleware(RequestDelegate next, IMemoryCache cache, AccountAccessCache keys)
{
    internal static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);

    private static readonly string[] AllowedSuffixes = ["/auth/logout", "/auth/refresh-token"];

    public async Task InvokeAsync(HttpContext context, IIdentityService identity)
    {
        var sub = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var path = context.Request.Path.Value ?? string.Empty;
        if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(sub, out var userId)
            && !AllowedSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
        {
            var block = await cache.GetOrCreateAsync(keys.Key(userId), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return identity.GetAccessBlockAsync(userId, context.RequestAborted);
            });
            if (block is not null)
            {
                await block.ToProblem().ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }
}
