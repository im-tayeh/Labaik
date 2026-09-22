using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Identity;
using MediatR;

namespace Labaik.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IVerificationCodeService verificationCodeService)
    : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var createResult = await identityService.CreateUserAsync(
            request.FullName, request.Email, request.Password, cancellationToken);

        if (createResult.IsFailure)
        {
            return Result.Failure(createResult.Error!);
        }

        await verificationCodeService.GenerateAndSendAsync(
    createResult.Value, request.Email, VerificationPurpose.EmailConfirmation, cancellationToken);

        return Result.Success();
    }
}