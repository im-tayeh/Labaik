using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Common;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Admin.Commands.UpdateCompany;

// HTTP body: the create form without ownerEmail (the owner account cannot be changed here). Full replace.
public sealed record UpdateCompanyRequest : CompanyFields;

public sealed class UpdateCompanyRequestValidator : CompanyFieldsValidator<UpdateCompanyRequest>;

public sealed record UpdateCompanyCommand(Guid CompanyId, UpdateCompanyRequest Fields) : IRequest<Result<AdminCompanyDto>>;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Fields).NotNull().SetValidator(new UpdateCompanyRequestValidator());
    }
}

public sealed class UpdateCompanyCommandHandler(
    IAppDbContext context,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<UpdateCompanyCommand, Result<AdminCompanyDto>>
{
    public async Task<Result<AdminCompanyDto>> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == request.CompanyId, cancellationToken);
        if (company is null)
        {
            return CompanyErrors.NotFoundById;
        }

        var registration = request.Fields.CommercialRegistrationNumber.Trim();
        if (await context.Companies.AnyAsync(
                c => c.Id != company.Id && c.CommercialRegistrationNumber == registration, cancellationToken))
        {
            return CompanyErrors.RegistrationNumberTaken;
        }

        var update = company.Update(request.Fields.ToDetails());
        if (update.IsFailure)
        {
            return update.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await company.ToAdminDtoAsync(context, userDirectory, timeProvider.TodayUtc(), cancellationToken);
    }
}
