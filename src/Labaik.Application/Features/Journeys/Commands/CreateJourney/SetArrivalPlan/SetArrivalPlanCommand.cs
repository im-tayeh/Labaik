using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Commands.SetArrivalPlan;

public sealed record SetArrivalPlanCommand(Guid JourneyId, TransportMode Mode, string EntryPoint)
    : IRequest<Result>;

public sealed class SetArrivalPlanCommandValidator : AbstractValidator<SetArrivalPlanCommand>
{
    public SetArrivalPlanCommandValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.EntryPoint).NotEmpty().MaximumLength(200);
    }
}

public sealed class SetArrivalPlanCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<SetArrivalPlanCommand, Result>
{
    public async Task<Result> Handle(SetArrivalPlanCommand request, CancellationToken cancellationToken)
    {
        var journey = await context.Journeys
            .FirstOrDefaultAsync(j => j.Id == request.JourneyId, cancellationToken);

        if (journey is null || journey.UserId != currentUser.Id)
        {
            return Result.Failure(Error.NotFound("Journey.NotFound", "Journey not found."));
        }

        var result = journey.SetArrivalPlan(request.Mode, request.EntryPoint);
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}