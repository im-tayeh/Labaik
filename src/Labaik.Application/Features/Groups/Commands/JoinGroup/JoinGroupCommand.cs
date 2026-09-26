using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Commands.JoinGroup;

public sealed record JoinGroupCommand(string JoinCode) : IRequest<Result<GroupSummaryDto>>;

public sealed class JoinGroupCommandValidator : AbstractValidator<JoinGroupCommand>
{
    public JoinGroupCommandValidator()
        => RuleFor(x => x.JoinCode).NotEmpty().MaximumLength(20);
}

public sealed class JoinGroupCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<JoinGroupCommand, Result<GroupSummaryDto>>
{
    public async Task<Result<GroupSummaryDto>> Handle(JoinGroupCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var code = request.JoinCode.Trim().ToUpperInvariant();

        var group = await context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.JoinCode == code, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("Group.NotFound", "No group found for this code.");
        }

        var result = group.Join(userId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);

        var leaderName = await userDirectory.GetFullNameAsync(group.LeaderId, cancellationToken);
        return new GroupSummaryDto(group.Id, group.Name, leaderName, group.MemberCount);
    }
}