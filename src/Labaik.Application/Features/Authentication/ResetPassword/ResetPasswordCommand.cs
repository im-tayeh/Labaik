using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty().Matches(@"^[0-9]{6}$");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public sealed class ResetPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        => identityService.ResetPasswordAsync(request.Email, request.Code, request.NewPassword, cancellationToken);
}