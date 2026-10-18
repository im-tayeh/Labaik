using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;

namespace Labaik.Application.Features.FamilyCircles.Commands.CreateFamilyCircle;

// Any signed-in user creates a circle and becomes its owner; share the returned joinCode.
public sealed record CreateFamilyCircleCommand(string Name) : IRequest<Result<FamilyCircleDto>>;

public sealed class CreateFamilyCircleCommandValidator : AbstractValidator<CreateFamilyCircleCommand>
{
    public CreateFamilyCircleCommandValidator() =>
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FamilyCircle.NameMaxLength);
}

public sealed class CreateFamilyCircleCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IJoinCodeGenerator codeGenerator,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<CreateFamilyCircleCommand, Result<FamilyCircleDto>>
{
    public async Task<Result<FamilyCircleDto>> Handle(CreateFamilyCircleCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var code = await context.GenerateUniqueCircleCodeAsync(codeGenerator, cancellationToken);
        var create = FamilyCircle.Create(request.Name, userId, code, timeProvider.GetUtcNow());
        if (create.IsFailure)
        {
            return create.Error!;
        }

        context.FamilyCircles.Add(create.Value);
        await context.SaveChangesAsync(cancellationToken);
        return await create.Value.ToDtoAsync(userId, context, userDirectory, cancellationToken);
    }
}
