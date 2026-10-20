using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Groups.Commands.AddGroupMember;

// HTTP body; the group comes from the route. A registered email is added as is. An unknown email
// gets a new confirmed pilgrim account (no role) with an emailed temporary password; fullName and
// phoneNumber are used only then (fullName defaults to the email).
public sealed record AddGroupMemberRequest(string Email, string? FullName = null, string? PhoneNumber = null);

public sealed record AddGroupMemberCommand(Guid GroupId, string Email, string? FullName = null, string? PhoneNumber = null)
    : IRequest<Result<CompanyGroupDto>>;

public sealed class AddGroupMemberCommandValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(CompanyErrors.EmailMaxLength);
        RuleFor(x => x.FullName).MaximumLength(CompanyErrors.ContactNameMaxLength);
        RuleFor(x => x.PhoneNumber).MaximumLength(CompanyErrors.PhoneMaxLength);
    }
}

public sealed class AddGroupMemberCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IIdentityService identityService,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    IOptions<AppLinksSettings> appLinks,
    TimeProvider timeProvider,
    ILogger<AddGroupMemberCommandHandler> logger) : IRequestHandler<AddGroupMemberCommand, Result<CompanyGroupDto>>
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

        return await GroupMembership.AddByEmailAsync(group, request.Email, request.FullName, request.PhoneNumber,
            addedBySupervisorId: null, context, identityService, userDirectory, emailSender, appLinks,
            timeProvider, logger, cancellationToken);
    }
}
