using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Admin.Queries.FindUserByEmail;

// Admin only (enforced by the endpoint policy): look up an account to manage its roles.
public sealed record FindUserByEmailQuery(string Email) : IRequest<Result<UserAccountDto>>;

public sealed class FindUserByEmailQueryValidator : AbstractValidator<FindUserByEmailQuery>
{
    public FindUserByEmailQueryValidator()
        => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class FindUserByEmailQueryHandler(
    IIdentityService identityService) : IRequestHandler<FindUserByEmailQuery, Result<UserAccountDto>>
{
    public Task<Result<UserAccountDto>> Handle(FindUserByEmailQuery request, CancellationToken cancellationToken)
        => identityService.FindAccountByEmailAsync(request.Email.Trim(), cancellationToken);
}
