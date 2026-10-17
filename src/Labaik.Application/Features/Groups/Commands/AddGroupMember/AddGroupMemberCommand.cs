using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.AddGroupMember;

// HTTP body; the group comes from the route. The pilgrim must already have an account.
public sealed record AddGroupMemberRequest(string Email);

public sealed record AddGroupMemberCommand(Guid GroupId, string Email) : IRequest<Result<CompanyGroupDto>>;

public sealed class AddGroupMemberCommandValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class AddGroupMemberCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<AddGroupMemberCommand, Result<CompanyGroupDto>>
{
    public async Task<Result<CompanyGroupDto>> Handle(AddGroupMemberCommand request, CancellationToken cancellationToken)
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

        var add = group.AddMember(account.Value.Id, timeProvider.GetUtcNow());
        if (add.IsFailure)
        {
            return add.Error!;
        }

        await context.SaveChangesAsync(cancellationToken); // MemberJoinedGroup -> supervisor notification
        return await group.ToCompanyDtoAsync(userDirectory, cancellationToken);
    }
}
