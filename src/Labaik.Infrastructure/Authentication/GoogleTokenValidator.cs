using Google.Apis.Auth;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Infrastructure.Authentication;

internal sealed class GoogleTokenValidator(
    IOptions<GoogleAuthSettings> options,
    ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    public const string ProviderName = "Google";

    private readonly GoogleAuthSettings _settings = options.Value;

    public async Task<Result<ExternalIdentity>> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientIds = _settings.ClientIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        if (clientIds.Length == 0)
        {
            logger.LogError("Google sign-in requested but Authentication:Google:ClientIds is not configured.");
            return Error.Failure("Auth.GoogleNotConfigured", "Google sign-in is not available.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Verifies signature (Google certs), issuer, expiry, and that the audience is one of our clients.
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = clientIds });
        }
        catch (InvalidJwtException ex)
        {
            logger.LogInformation("Rejected Google ID token: {Reason}", ex.Message);
            return Error.Unauthorized("Auth.InvalidGoogleToken", "The Google token is invalid or expired.");
        }

        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
        {
            return Error.Unauthorized("Auth.InvalidGoogleToken", "The Google token is missing required claims.");
        }

        return new ExternalIdentity(ProviderName, payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
    }
}
