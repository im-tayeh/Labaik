using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Guides.Dtos;
using Labaik.Application.Features.Guides.Mappers;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Guides.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Guides.Queries.GetGuideArticles;

public sealed record GetGuideArticlesQuery(GuideCategory Category) : IRequest<Result<IReadOnlyList<GuideArticleDto>>>;

public sealed class GetGuideArticlesQueryValidator : AbstractValidator<GetGuideArticlesQuery>
{
    public GetGuideArticlesQueryValidator()
        => RuleFor(x => x.Category).IsInEnum();
}

public sealed class GetGuideArticlesQueryHandler(
    IAppDbContext context) : IRequestHandler<GetGuideArticlesQuery, Result<IReadOnlyList<GuideArticleDto>>>
{
    public async Task<Result<IReadOnlyList<GuideArticleDto>>> Handle(GetGuideArticlesQuery request, CancellationToken cancellationToken)
    {
        var articles = await context.GuideArticles
            .AsNoTracking()
            .Where(a => a.Category == request.Category && a.IsPublished)
            .OrderBy(a => a.Order)
            .ToListAsync(cancellationToken);

        return articles.Select(a => a.ToDto()).ToList();
    }
}
