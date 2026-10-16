using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.GoogleSignIn;

// IdToken: the Google ID token obtained by the Flutter app (google_sign_in).
public sealed record GoogleSignInCommand(string IdToken) : IRequest<Result<AuthTokens>>;
