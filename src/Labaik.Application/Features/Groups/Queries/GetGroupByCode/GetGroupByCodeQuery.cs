using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Queries.GetGroupByCode;

public sealed record GetGroupByCodeQuery(string JoinCode) : IRequest<Result<GroupSummaryDto>>;

public sealed class GetGroupByCodeQueryHandler(
    IAppDbContext context,
    IUserDirectory userDirectory) : IRequestHandler<GetGroupByCodeQuery, Result<GroupSummaryDto>>
{
    public async Task<Result<GroupSummaryDto>> Handle(GetGroupByCodeQuery request, CancellationToken cancellationToken)
    {
        var code = request.JoinCode.Trim().ToUpperInvariant();

        var group = await context.Groups
            .Include(g => g.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.JoinCode == code, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("Group.NotFound", "No group found for this code.");
        }

        var leaderName = await userDirectory.GetFullNameAsync(group.LeaderId, cancellationToken);
        return new GroupSummaryDto(group.Id, group.Name, leaderName, group.MemberCount);
    }
}