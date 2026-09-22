using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.VerifyEmail;

public sealed class VerifyEmailCommandHandler(
    IIdentityService identityService,
    IAuthTokenFactory authTokenFactory) : IRequestHandler<VerifyEmailCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var result = await identityService.VerifyEmailAsync(request.Email, request.Code, cancellationToken);
        return result.IsFailure ? result.Error! : await authTokenFactory.CreateAsync(result.Value, cancellationToken);
    }
}