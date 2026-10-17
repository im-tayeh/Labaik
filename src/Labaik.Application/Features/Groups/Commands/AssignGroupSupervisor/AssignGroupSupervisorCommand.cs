using FluentValidation;
using Labaik.Application.Common.Authorization;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.AssignGroupSupervisor;

// HTTP body; the group comes from the route. The supervisor must already have an account.
public sealed record AssignGroupSupervisorRequest(string Email);

// Sets the group's supervisor (replacing any previous one) and grants the global Supervisor role,
// effective from the supervisor's next token.
public sealed record AssignGroupSupervisorCommand(Guid GroupId, string Email) : IRequest<Result<CompanyGroupDto>>;

public sealed class AssignGroupSupervisorCommandValidator : AbstractValidator<AssignGroupSupervisorCommand>
{
    public AssignGroupSupervisorCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class AssignGroupSupervisorCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<AssignGroupSupervisorCommand, Result<CompanyGroupDto>>
{
    public async Task<Result<CompanyGroupDto>> Handle(AssignGroupSupervisorCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var group = await context.GetCompanyGroupAsync(request.GroupId, companyId.Value, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var account = await identityService.FindAccountByEmailAsync(request.Email.Trim(), cancellationToken);
        if (account.IsFailure)
        {
            return account.Error!;
        }

        var assign = group.AssignSupervisor(account.Value.Id, timeProvider.GetUtcNow());
        if (assign.IsFailure)
        {
            return assign.Error!;
        }

        // Idempotent role grant first, then one save for the group.
        var role = await identityService.AddToRoleAsync(account.Value.Id, Roles.Supervisor, cancellationToken);
        if (role.IsFailure)
        {
            return role.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await group.ToCompanyDtoAsync(userDirectory, cancellationToken);
    }
}
