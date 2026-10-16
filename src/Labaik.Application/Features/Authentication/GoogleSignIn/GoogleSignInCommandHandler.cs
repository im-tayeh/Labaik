using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.GoogleSignIn;

public sealed class GoogleSignInCommandHandler(
    IGoogleTokenValidator googleTokenValidator,
    IIdentityService identityService,
    IAuthTokenFactory authTokenFactory) : IRequestHandler<GoogleSignInCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        var identity = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity.IsFailure)
        {
            return identity.Error!;
        }

        var user = await identityService.ExternalSignInAsync(identity.Value, cancellationToken);
        return user.IsFailure ? user.Error! : await authTokenFactory.CreateAsync(user.Value, cancellationToken);
    }
}
