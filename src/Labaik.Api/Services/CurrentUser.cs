using System.Security.Claims;
using Labaik.Application.Common.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Labaik.Api.Services;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id
    {
        get
        {
            var sub = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}