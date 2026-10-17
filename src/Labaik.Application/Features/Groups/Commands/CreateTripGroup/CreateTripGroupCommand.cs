using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Commands.CreateTripGroup;

// HTTP body; the trip comes from the route.
public sealed record CreateTripGroupRequest(string Name);

// A company creates a group under one of its trips; it starts without a supervisor.
public sealed record CreateTripGroupCommand(Guid TripId, string Name) : IRequest<Result<CompanyGroupDto>>;

public sealed class CreateTripGroupCommandValidator : AbstractValidator<CreateTripGroupCommand>
{
    public CreateTripGroupCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(GroupErrors.NameMaxLength);
    }
}

public sealed class CreateTripGroupCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IJoinCodeGenerator codeGenerator,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<CreateTripGroupCommand, Result<CompanyGroupDto>>
{
    public async Task<Result<CompanyGroupDto>> Handle(CreateTripGroupCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure)
        {
            return companyId.Error!;
        }

        var ownsTrip = await context.Trips
            .AnyAsync(t => t.Id == request.TripId && t.CompanyId == companyId.Value, cancellationToken);
        if (!ownsTrip)
        {
            return TripErrors.NotFound;
        }

        var code = await context.GenerateUniqueJoinCodeAsync(codeGenerator, cancellationToken);
        var create = Group.CreateForTrip(request.Name, code, companyId.Value, request.TripId, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.Groups.Add(create.Value);
        await context.SaveChangesAsync(cancellationToken);

        return await create.Value.ToCompanyDtoAsync(userDirectory, cancellationToken);
    }
}
