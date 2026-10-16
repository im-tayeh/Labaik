namespace Labaik.Infrastructure.Authentication;

public sealed class GoogleAuthSettings
{
    public const string SectionName = "Authentication:Google";

    // OAuth client ids allowed as the ID token audience (Android, iOS, Web).
    // Env vars: Authentication__Google__ClientIds__0, __1, ...
    public string[] ClientIds { get; init; } = [];
}
