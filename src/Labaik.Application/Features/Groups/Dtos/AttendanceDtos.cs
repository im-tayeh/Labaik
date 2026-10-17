using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;

namespace Labaik.Application.Features.Groups.Dtos;

public sealed record AttendanceRowDto(Guid UserId, string? FullName, AttendanceStatus Status, DateTimeOffset? ConfirmedAtUtc);

// Supervisor's roll call: pilgrims only (the supervisor is not listed).
public sealed record GroupAttendanceDto(int PresentCount, int AbsentCount, IReadOnlyList<AttendanceRowDto> Members);

public static class AttendanceMapper
{
    public static async Task<GroupAttendanceDto> ToAttendanceDtoAsync(
        this Group group, IUserDirectory userDirectory, CancellationToken cancellationToken)
    {
        var pilgrims = group.Members.Where(m => m.Role == GroupMemberRole.Member).ToList();
        var names = await userDirectory.GetFullNamesAsync(pilgrims.Select(m => m.UserId).ToList(), cancellationToken);

        var rows = pilgrims
            .OrderBy(m => m.AttendanceStatus) // present first
            .ThenBy(m => names.GetValueOrDefault(m.UserId))
            .Select(m => new AttendanceRowDto(m.UserId, names.GetValueOrDefault(m.UserId), m.AttendanceStatus, m.AttendanceConfirmedAtUtc))
            .ToList();

        var present = rows.Count(r => r.Status == AttendanceStatus.Present);
        return new GroupAttendanceDto(present, rows.Count - present, rows);
    }
}
