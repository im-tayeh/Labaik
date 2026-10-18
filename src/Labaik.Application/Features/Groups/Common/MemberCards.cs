using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using Labaik.Domain.Profiles.Enums;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Groups.Common;

// Member card for the group's supervisor and the owning company only. Contains HealthNotes (sensitive):
// never reuse this DTO in lists or in anything a pilgrim or another member can read.
public sealed record GroupMemberDetailsDto(
    Guid UserId,
    string? FullName,
    string? Email,
    GroupMemberRole Role,
    int? MemberNumberInGroup,
    Gender? Gender,
    string? Nationality,
    string? PhotoUrl,
    string? HealthNotes,
    string? Notes,
    AttendanceStatus AttendanceStatus,
    DateTimeOffset JoinedAtUtc);

// Member list row (supervisor / company). No health notes and no supervisor notes.
public sealed record GroupMemberRowDto(
    Guid UserId,
    string? FullName,
    string? Email,
    GroupMemberRole Role,
    int? MemberNumberInGroup,
    Gender? Gender,
    string? Nationality,
    string? PhotoUrl,
    AttendanceStatus AttendanceStatus,
    DateTimeOffset JoinedAtUtc);

internal static class MemberCards
{
    public static async Task<GroupMemberDetailsDto?> GetMemberDetailsAsync(
        this IAppDbContext context, Group group, Guid memberUserId, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var member = group.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (member is null)
        {
            return null;
        }

        var contact = await userDirectory.GetContactAsync(member.UserId, cancellationToken);
        var profile = await context.PilgrimProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == member.UserId, cancellationToken);

        return new GroupMemberDetailsDto(
            member.UserId, contact?.FullName, contact?.Email, member.Role, member.MemberNumber,
            profile?.Gender, profile?.Nationality, profile?.PhotoUrl, profile?.HealthNotes,
            member.Notes, member.AttendanceStatus, member.JoinedAtUtc);
    }

    // Supervisor first, then pilgrims by member number.
    public static async Task<IReadOnlyList<GroupMemberRowDto>> GetMemberRowsAsync(
        this IAppDbContext context, Group group, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var ids = group.Members.Select(m => m.UserId).ToList();
        var contacts = await userDirectory.GetContactsAsync(ids, cancellationToken);
        var profiles = await context.PilgrimProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .Select(p => new { p.UserId, p.Gender, p.Nationality, p.PhotoUrl })
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        return group.Members
            .OrderBy(m => m.Role == GroupMemberRole.Leader ? 0 : 1)
            .ThenBy(m => m.MemberNumber ?? int.MaxValue)
            .Select(m =>
            {
                var contact = contacts.GetValueOrDefault(m.UserId);
                var profile = profiles.GetValueOrDefault(m.UserId);
                return new GroupMemberRowDto(
                    m.UserId, contact?.FullName, contact?.Email, m.Role, m.MemberNumber,
                    profile?.Gender, profile?.Nationality, profile?.PhotoUrl, m.AttendanceStatus, m.JoinedAtUtc);
            })
            .ToList();
    }
}
