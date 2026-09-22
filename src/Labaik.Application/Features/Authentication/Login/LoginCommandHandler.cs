using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    IAuthTokenFactory authTokenFactory) : IRequestHandler<LoginCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await identityService.LoginAsync(request.Email, request.Password, cancellationToken);
        return result.IsFailure ? result.Error! : await authTokenFactory.CreateAsync(result.Value, cancellationToken);
    }
}