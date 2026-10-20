using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Provisioning;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Application.Features.Groups.Commands.SupervisorAddMember;

public sealed record SupervisorAddMemberCommand(Guid GroupId, string Email, string? FullName = null, string? PhoneNumber = null)
    : IRequest<Result<CompanyGroupDto>>;

public sealed class SupervisorAddMemberCommandValidator : AbstractValidator<SupervisorAddMemberCommand>
{
    public SupervisorAddMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
    }
}

public sealed class SupervisorAddMemberCommandHandler(
    IAppDbContext context, ICurrentUser currentUser, IIdentityService identityService,
    IUserDirectory userDirectory, IEmailSender emailSender, IOptions<AppLinksSettings> appLinks,
    TimeProvider timeProvider, ILogger<SupervisorAddMemberCommandHandler> logger)
    : IRequestHandler<SupervisorAddMemberCommand, Result<CompanyGroupDto>>
{
    public async Task<Result<CompanyGroupDto>> Handle(SupervisorAddMemberCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } supervisorId) return Error.Unauthorized("Auth.Required", "Authentication is required.");
        var group = await context.GetGroupForMemberAsync(request.GroupId, supervisorId, cancellationToken);
        if (group is null) return GroupErrors.NotFound;
        if (group.LeaderId != supervisorId) return GroupErrors.NotLeader;

        return await GroupMembership.AddByEmailAsync(group, request.Email, request.FullName, request.PhoneNumber,
            supervisorId, context, identityService, userDirectory, emailSender, appLinks,
            timeProvider, logger, cancellationToken);
    }
}
