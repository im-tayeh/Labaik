using Labaik.Application.Features.Guides.Dtos;
using Labaik.Domain.Guides;

namespace Labaik.Application.Features.Guides.Mappers;

public static class GuideMapper
{
    public static GuideArticleDto ToDto(this GuideArticle a) => new(
        a.Id, a.Category, a.Order, a.Title, a.Body, a.Reference);
}
