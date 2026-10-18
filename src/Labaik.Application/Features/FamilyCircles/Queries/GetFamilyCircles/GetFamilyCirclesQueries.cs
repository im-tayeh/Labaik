using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Common;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.FamilyCircles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.FamilyCircles.Queries.GetFamilyCircles;

// Circles I am in, oldest first.
public sealed record GetMyFamilyCirclesQuery : IRequest<Result<IReadOnlyList<FamilyCircleSummaryDto>>>;

public sealed class GetMyFamilyCirclesQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyFamilyCirclesQuery, Result<IReadOnlyList<FamilyCircleSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<FamilyCircleSummaryDto>>> Handle(
        GetMyFamilyCirclesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var circles = await context.FamilyCircles.AsNoTracking()
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return circles
            .Select(c => new FamilyCircleSummaryDto(
                c.Id, c.Name, c.JoinCode, c.Members.First(m => m.UserId == userId).Role, c.Members.Count))
            .ToList();
    }
}

// Ids for the location hub subscription.
public sealed record GetMyFamilyCircleIdsQuery : IRequest<Result<IReadOnlyList<Guid>>>;

public sealed class GetMyFamilyCircleIdsQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyFamilyCircleIdsQuery, Result<IReadOnlyList<Guid>>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetMyFamilyCircleIdsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        return await context.FamilyCircles
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetFamilyCircleQuery(Guid CircleId) : IRequest<Result<FamilyCircleDto>>;

public sealed class GetFamilyCircleQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<GetFamilyCircleQuery, Result<FamilyCircleDto>>
{
    public async Task<Result<FamilyCircleDto>> Handle(GetFamilyCircleQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var circle = await context.GetCircleForMemberAsync(request.CircleId, userId, cancellationToken);
        if (circle is null)
        {
            return FamilyCircleErrors.NotFound;
        }

        return await circle.ToDtoAsync(userId, context, userDirectory, cancellationToken);
    }
}

// Before joining: name, owner and size of the circle behind a code.
public sealed record GetFamilyCirclePreviewQuery(string Code) : IRequest<Result<FamilyCirclePreviewDto>>;

public sealed class GetFamilyCirclePreviewQueryHandler(
    IAppDbContext context,
    IUserDirectory userDirectory) : IRequestHandler<GetFamilyCirclePreviewQuery, Result<FamilyCirclePreviewDto>>
{
    public async Task<Result<FamilyCirclePreviewDto>> Handle(GetFamilyCirclePreviewQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var circle = await context.FamilyCircles.AsNoTracking()
            .Where(c => c.JoinCode == code)
            .Select(c => new { c.Id, c.Name, c.OwnerId, Count = c.Members.Count })
            .FirstOrDefaultAsync(cancellationToken);
        if (circle is null)
        {
            return FamilyCircleErrors.NotFound;
        }

        var ownerName = await userDirectory.GetFullNameAsync(circle.OwnerId, cancellationToken);
        return new FamilyCirclePreviewDto(circle.Id, circle.Name, ownerName, circle.Count);
    }
}
