using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.FamilyCircles.Commands.JoinFamilyCircle;

// Join with a code shared by a member. Already in it -> 409; full -> 409; unknown code -> 404.
public sealed record JoinFamilyCircleCommand(string Code) : IRequest<Result<FamilyCircleDto>>;

public sealed class JoinFamilyCircleCommandValidator : AbstractValidator<JoinFamilyCircleCommand>
{
    public JoinFamilyCircleCommandValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
}

public sealed class JoinFamilyCircleCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<JoinFamilyCircleCommand, Result<FamilyCircleDto>>
{
    public async Task<Result<FamilyCircleDto>> Handle(JoinFamilyCircleCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var circle = await context.FamilyCircles.FirstOrDefaultAsync(c => c.JoinCode == code, cancellationToken);
        if (circle is null)
        {
            return FamilyCircleErrors.NotFound;
        }

        var join = circle.Join(userId, timeProvider.GetUtcNow());
        if (join.IsFailure)
        {
            return join.Error!;
        }

        await context.SaveChangesAsync(cancellationToken); // FamilyMemberJoined -> owner notified
        return await circle.ToDtoAsync(userId, context, userDirectory, cancellationToken);
    }
}
