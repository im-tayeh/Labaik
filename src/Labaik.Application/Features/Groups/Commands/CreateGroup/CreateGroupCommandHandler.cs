using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Commands.CreateGroup;

public sealed class CreateGroupCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IJoinCodeGenerator codeGenerator,
    TimeProvider timeProvider) : IRequestHandler<CreateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        // Generate a unique join code (retry on the rare collision).
        string code;
        var attempts = 0;
        do
        {
            code = codeGenerator.Generate();
            attempts++;
        }
        while (await context.Groups.AnyAsync(g => g.JoinCode == code, cancellationToken) && attempts < 5);

        var result = Group.Create(request.Name, code, userId, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result.Error!;
        }

        context.Groups.Add(result.Value);
        await context.SaveChangesAsync(cancellationToken);

        var g = result.Value;
        return new GroupDto(g.Id, g.Name, g.JoinCode, g.LeaderId,
            g.Members.Select(m => new GroupMemberDto(m.UserId, m.Role, m.JoinedAtUtc)).ToList());
    }
}