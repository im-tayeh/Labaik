using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthTokens>>;
