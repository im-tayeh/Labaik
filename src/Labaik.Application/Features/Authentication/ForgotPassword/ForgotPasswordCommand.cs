using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ForgotPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        => identityService.ForgotPasswordAsync(request.Email, cancellationToken);
}