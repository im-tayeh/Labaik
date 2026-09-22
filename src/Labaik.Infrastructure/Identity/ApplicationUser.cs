using Microsoft.AspNetCore.Identity;

namespace Labaik.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
}