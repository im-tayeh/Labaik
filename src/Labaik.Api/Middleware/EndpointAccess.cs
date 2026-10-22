using Microsoft.AspNetCore.Authorization;

namespace Labaik.Api.Middleware;

internal static class EndpointAccess
{
    // Public endpoints (login, forgot/reset password, Google, ...) and unmatched routes: an old token sent
    // along must not block them, so the account checks only guard endpoints that require authorization.
    public static bool RequiresAuthorization(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        return metadata is not null
            && metadata.GetMetadata<IAllowAnonymous>() is null
            && metadata.GetMetadata<IAuthorizeData>() is not null;
    }
}
