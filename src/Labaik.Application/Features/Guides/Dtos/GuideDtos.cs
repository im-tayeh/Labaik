using Labaik.Domain.Guides.Enums;

namespace Labaik.Application.Features.Guides.Dtos;

public sealed record GuideArticleDto(
    Guid Id,
    GuideCategory Category,
    int Order,
    string Title,
    string Body,
    string? Reference);
