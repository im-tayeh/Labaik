using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokens>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenService refreshTokenService,
    IIdentityService identityService,
    IAuthTokenFactory authTokenFactory) : IRequestHandler<RefreshTokenCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var validation = await refreshTokenService.ValidateAndConsumeAsync(request.RefreshToken, cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        var userResult = await identityService.GetAuthUserByIdAsync(validation.Value, cancellationToken);
        return userResult.IsFailure ? userResult.Error! : await authTokenFactory.CreateAsync(userResult.Value, cancellationToken);
    }
}