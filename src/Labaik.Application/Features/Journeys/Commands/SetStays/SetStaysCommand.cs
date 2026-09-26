using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Journeys.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Journeys.Commands.SetStays;

public sealed record StayInput(string CityName, AccommodationType AccommodationType, string PlaceName, DateTimeOffset ArrivalAtUtc);

public sealed record SetStaysCommand(Guid JourneyId, List<StayInput> Stays) : IRequest<Result>;

public sealed class SetStaysCommandValidator : AbstractValidator<SetStaysCommand>
{
    public SetStaysCommandValidator()
    {
        RuleFor(x => x.Stays).NotEmpty();
        RuleForEach(x => x.Stays).ChildRules(s =>
        {
            s.RuleFor(i => i.CityName).IsInEnum();
            s.RuleFor(i => i.AccommodationType).IsInEnum();
            s.RuleFor(i => i.PlaceName).NotEmpty().MaximumLength(200);
        });
    }
}

public sealed class SetStaysCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<SetStaysCommand, Result>
{
    public async Task<Result> Handle(SetStaysCommand request, CancellationToken cancellationToken)
    {
        var journey = await context.Journeys
            .Include(j => j.Stays)
            .FirstOrDefaultAsync(j => j.Id == request.JourneyId, cancellationToken);

        if (journey is null || journey.UserId != currentUser.Id)
        {
            return Result.Failure(Error.NotFound("Journey.NotFound", "Journey not found."));
        }

        var clear = journey.ClearStays();
        if (clear.IsFailure)
        {
            return clear;
        }

        foreach (var s in request.Stays)
        {
            var add = journey.AddStay(s.CityName, s.AccommodationType, s.PlaceName, s.ArrivalAtUtc);
            if (add.IsFailure)
            {
                return add;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}