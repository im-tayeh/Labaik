namespace Labaik.Application.Features.Chat.Dtos;

public sealed record ChatMessageDto(
    Guid Id,
    Guid GroupId,
    Guid SenderId,
    string? SenderName,
    string Body,
    DateTimeOffset SentAtUtc);
