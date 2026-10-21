using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Authentication.Logout;

public sealed record LogoutCommand(string RefreshToken, string? DeviceToken = null) : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
        RuleFor(x => x.DeviceToken).MaximumLength(Labaik.Domain.Notifications.DeviceToken.TokenMaxLength);
    }
}

public sealed class LogoutCommandHandler(IRefreshTokenService refreshTokenService, IAppDbContext context)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            var devices = await context.DeviceTokens.Where(x => x.Token == request.DeviceToken).ToListAsync(cancellationToken);
            if (devices.Count > 0)
            {
                context.DeviceTokens.RemoveRange(devices);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        return Result.Success();
    }
}
