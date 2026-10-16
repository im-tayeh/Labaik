namespace Labaik.Application.Features.Announcements.Dtos;

public sealed record AnnouncementDto(
    Guid Id,
    Guid GroupId,
    string Title,
    string Body,
    Guid AuthorId,
    string? AuthorName,
    DateTimeOffset CreatedAtUtc,
    bool IsRead);
