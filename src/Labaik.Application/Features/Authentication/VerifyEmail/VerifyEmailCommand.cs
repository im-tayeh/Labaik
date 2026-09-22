using Labaik.Application.Common.Models;
using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.VerifyEmail;

public sealed record VerifyEmailCommand(string Email, string Code) : IRequest<Result<AuthTokens>>;
