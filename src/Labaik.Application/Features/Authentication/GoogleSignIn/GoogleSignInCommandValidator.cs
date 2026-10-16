using FluentValidation;

namespace Labaik.Application.Features.Authentication.GoogleSignIn;

public sealed class GoogleSignInCommandValidator : AbstractValidator<GoogleSignInCommand>
{
    public GoogleSignInCommandValidator()
        => RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
}
