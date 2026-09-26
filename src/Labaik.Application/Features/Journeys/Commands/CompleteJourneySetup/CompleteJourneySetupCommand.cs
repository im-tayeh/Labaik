using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Commands.CompleteJourneySetup;

public sealed record CompleteJourneySetupCommand(Guid JourneyId) : IRequest<Result>;

public sealed class CompleteJourneySetupCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<CompleteJourneySetupCommand, Result>
{
    public async Task<Result> Handle(CompleteJourneySetupCommand request, CancellationToken cancellationToken)
    {
        var journey = await context.Journeys
            .Include(j => j.Stays)
            .FirstOrDefaultAsync(j => j.Id == request.JourneyId, cancellationToken);

        if (journey is null || journey.UserId != currentUser.Id)
        {
            return Result.Failure(Error.NotFound("Journey.NotFound", "Journey not found."));
        }

        var result = journey.MarkReady(); // enforces arrival plan + at least one stay
        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}