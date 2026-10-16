using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Guides.Dtos;
using Labaik.Application.Features.Guides.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Guides;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Guides.Queries.GetGuideArticleById;

public sealed record GetGuideArticleByIdQuery(Guid ArticleId) : IRequest<Result<GuideArticleDto>>;

public sealed class GetGuideArticleByIdQueryHandler(
    IAppDbContext context) : IRequestHandler<GetGuideArticleByIdQuery, Result<GuideArticleDto>>
{
    public async Task<Result<GuideArticleDto>> Handle(GetGuideArticleByIdQuery request, CancellationToken cancellationToken)
    {
        var article = await context.GuideArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ArticleId && a.IsPublished, cancellationToken);

        return article is null ? GuideErrors.NotFound : article.ToDto();
    }
}
