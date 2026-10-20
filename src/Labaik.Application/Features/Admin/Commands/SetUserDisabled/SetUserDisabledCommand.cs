using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Admin.Commands.SetUserDisabled;

public sealed record SetUserDisabledCommand(Guid UserId, bool Disabled) : IRequest<Result>;

public sealed class SetUserDisabledCommandHandler(IIdentityService identityService)
    : IRequestHandler<SetUserDisabledCommand, Result>
{
    public Task<Result> Handle(SetUserDisabledCommand request, CancellationToken cancellationToken)
        => identityService.SetDisabledAsync(request.UserId, request.Disabled, cancellationToken);
}
