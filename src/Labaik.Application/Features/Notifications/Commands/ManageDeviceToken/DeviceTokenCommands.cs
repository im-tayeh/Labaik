using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Notifications.Commands.ManageDeviceToken;

public sealed record DeviceTokenRequest(string Token, DevicePlatform Platform);
public sealed record RegisterDeviceTokenCommand(string Token, DevicePlatform Platform) : IRequest<Result>;
public sealed class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(DeviceToken.TokenMaxLength);
        RuleFor(x => x.Platform).IsInEnum();
    }
}
public sealed class RegisterDeviceTokenCommandHandler(IAppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<RegisterDeviceTokenCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceTokenCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var token = request.Token.Trim();
        var existing = await context.DeviceTokens.FirstOrDefaultAsync(x => x.Token == token, ct);
        if (existing is null)
        {
            var created = DeviceToken.Create(userId, token, request.Platform, timeProvider.GetUtcNow());
            if (created.IsFailure) return Result.Failure(created.Error!);
            context.DeviceTokens.Add(created.Value);
        }
        else
        {
            var update = existing.Update(userId, request.Platform, timeProvider.GetUtcNow());
            if (update.IsFailure) return update;
        }
        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record RemoveDeviceTokenCommand(string Token) : IRequest<Result>;
public sealed class RemoveDeviceTokenCommandValidator : AbstractValidator<RemoveDeviceTokenCommand>
{ public RemoveDeviceTokenCommandValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(DeviceToken.TokenMaxLength); }
public sealed class RemoveDeviceTokenCommandHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<RemoveDeviceTokenCommand, Result>
{
    public async Task<Result> Handle(RemoveDeviceTokenCommand request, CancellationToken ct)
    {
        if (currentUser.Id is not { } userId) return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        var entity = await context.DeviceTokens.FirstOrDefaultAsync(x => x.UserId == userId && x.Token == request.Token, ct);
        if (entity is not null) { context.DeviceTokens.Remove(entity); await context.SaveChangesAsync(ct); }
        return Result.Success();
    }
}
