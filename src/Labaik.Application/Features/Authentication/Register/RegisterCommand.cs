using Labaik.Domain.Common.Results;
using MediatR;

namespace Labaik.Application.Features.Authentication.Register;

public sealed record RegisterCommand(string FullName, string Email, string Password) : IRequest<Result>;
