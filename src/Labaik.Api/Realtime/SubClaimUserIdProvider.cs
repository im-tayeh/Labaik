using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Labaik.Api.Realtime;

// Inbound claims are not mapped (MapInboundClaims = false), so SignalR's default NameIdentifier lookup
// finds nothing; identify users by the JWT "sub" claim for Clients.User(...).
internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
