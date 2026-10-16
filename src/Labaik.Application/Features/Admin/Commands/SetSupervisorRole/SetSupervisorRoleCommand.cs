using FluentValidation;
using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Admin.Commands.SetSupervisorRole;

// Admin only (enforced by the endpoint policy). Idempotent. Takes effect on the user's next
// token (login or refresh). Revoking does not remove them as leader of groups they already lead.
public sealed record SetSupervisorRoleCommand(Guid UserId, bool IsSupervisor) : IRequest<Result<UserAccountDto>>;

public sealed class SetSupervisorRoleCommandValidator : AbstractValidator<SetSupervisorRoleCommand>
{
    public SetSupervisorRoleCommandValidator()
        => RuleFor(x => x.UserId).NotEmpty();
}

public sealed class SetSupervisorRoleCommandHandler(
    IIdentityService identityService) : IRequestHandler<SetSupervisorRoleCommand, Result<UserAccountDto>>
{
    public Task<Result<UserAccountDto>> Handle(SetSupervisorRoleCommand request, CancellationToken cancellationToken)
        => request.IsSupervisor
            ? identityService.AddToRoleAsync(request.UserId, Roles.Supervisor, cancellationToken)
            : identityService.RemoveFromRoleAsync(request.UserId, Roles.Supervisor, cancellationToken);
}
