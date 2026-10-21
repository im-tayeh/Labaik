using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.UpdateCompanyGroup;

public sealed record UpdateCompanyGroupRequest(string Name, string? Notes = null, int? Capacity = null);
public sealed record UpdateCompanyGroupCommand(Guid GroupId, string Name, string? Notes, int? Capacity)
    : IRequest<Result<CompanyGroupDto>>;

public sealed class UpdateCompanyGroupCommandValidator : AbstractValidator<UpdateCompanyGroupCommand>
{
    public UpdateCompanyGroupCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(GroupErrors.NameMaxLength);
        RuleFor(x => x.Notes).MaximumLength(Group.NotesMaxLength);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
    }
}

public sealed class UpdateCompanyGroupCommandHandler(
    IAppDbContext context, ICurrentUser currentUser, IUserDirectory userDirectory)
    : IRequestHandler<UpdateCompanyGroupCommand, Result<CompanyGroupDto>>
{
    public async Task<Result<CompanyGroupDto>> Handle(UpdateCompanyGroupCommand request, CancellationToken cancellationToken)
    {
        var companyId = await context.GetMyCompanyIdAsync(currentUser, cancellationToken);
        if (companyId.IsFailure) return companyId.Error!;
        var group = await context.GetCompanyGroupAsync(request.GroupId, companyId.Value, cancellationToken);
        if (group is null) return GroupErrors.NotFound;
        var update = group.UpdateDetails(request.Name, request.Notes, request.Capacity);
        if (update.IsFailure) return update.Error!;
        await context.SaveChangesAsync(cancellationToken);
        return await group.ToCompanyDtoAsync(userDirectory, cancellationToken);
    }
}
