using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.FamilyCircles.Dtos;
using Labaik.Domain.FamilyCircles;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.FamilyCircles.Common;

internal static class FamilyCircleDataAccess
{
    // A tracked circle with its members, or null when it does not exist or the user is not in it
    // (callers answer NotFound either way).
    public static async Task<FamilyCircle?> GetCircleForMemberAsync(
        this IAppDbContext context, Guid circleId, Guid userId, CancellationToken cancellationToken)
    {
        var circle = await context.FamilyCircles.FirstOrDefaultAsync(c => c.Id == circleId, cancellationToken);
        return circle is not null && circle.IsMember(userId) ? circle : null;
    }

    public static async Task<string> GenerateUniqueCircleCodeAsync(
        this IAppDbContext context, IJoinCodeGenerator codeGenerator, CancellationToken cancellationToken)
    {
        string code;
        var attempts = 0;
        do
        {
            code = codeGenerator.Generate();
            attempts++;
        }
        while (await context.FamilyCircles.AnyAsync(c => c.JoinCode == code, cancellationToken) && attempts < 5);

        return code;
    }

    // Circles where the user currently shows their location (realtime fan-out targets).
    public static Task<List<Guid>> GetCirclesSharingLocationOfAsync(
        this IAppDbContext context, Guid userId, CancellationToken cancellationToken)
        => context.FamilyCircles
            .Where(c => c.Members.Any(m => m.UserId == userId && m.IsSharingLocation))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

    public static async Task<FamilyCircleDto> ToDtoAsync(
        this FamilyCircle circle, Guid viewerId, IAppDbContext context, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var ids = circle.Members.Select(m => m.UserId).ToList();
        var names = await userDirectory.GetFullNamesAsync(ids, cancellationToken);
        var photos = await context.PilgrimProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.PhotoUrl != null)
            .ToDictionaryAsync(p => p.UserId, p => p.PhotoUrl, cancellationToken);

        var members = circle.Members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .Select(m => new FamilyCircleMemberDto(
                m.UserId, names.GetValueOrDefault(m.UserId), photos.GetValueOrDefault(m.UserId), m.Role, m.IsSharingLocation, m.JoinedAtUtc))
            .ToList();

        var myRole = circle.Members.First(m => m.UserId == viewerId).Role;
        return new FamilyCircleDto(circle.Id, circle.Name, circle.JoinCode, circle.OwnerId, myRole, members);
    }
}
