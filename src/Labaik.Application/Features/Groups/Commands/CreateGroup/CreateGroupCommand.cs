using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Labaik.Application.Features.Groups.Commands.CreateGroup;

public sealed record CreateGroupCommand(string Name) : IRequest<Result<GroupDto>>;