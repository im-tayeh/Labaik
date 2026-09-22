using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.ResendCode;

public sealed record ResendCodeCommand(string Email) : IRequest<Result>;

public sealed class ResendCodeCommandValidator : AbstractValidator<ResendCodeCommand>
{
    public ResendCodeCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ResendCodeCommandHandler(IIdentityService identityService)
    : IRequestHandler<ResendCodeCommand, Result>
{
    public Task<Result> Handle(ResendCodeCommand request, CancellationToken cancellationToken)
        => identityService.ResendEmailConfirmationAsync(request.Email, cancellationToken);
}