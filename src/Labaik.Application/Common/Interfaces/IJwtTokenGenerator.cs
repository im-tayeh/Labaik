using Labaik.Application.Common.Models;

namespace Labaik.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    AccessToken GenerateAccessToken(Guid userId, string email, string fullName, IEnumerable<string> roles);
}