using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Guides.Enums;

namespace Labaik.Domain.Guides;

// One educational article. Seeded data (pending religious approval), not user data.
public sealed class GuideArticle : AggregateRoot
{
    public GuideCategory Category { get; private set; }
    public int Order { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Reference { get; private set; }
    public bool IsPublished { get; private set; }

    private GuideArticle() { } // EF

    private GuideArticle(GuideCategory category, int order)
    {
        Category = category;
        Order = order;
    }

    public static Result<GuideArticle> Create(
        GuideCategory category, int order, string title, string body, string? reference, bool isPublished)
    {
        if (order <= 0)
        {
            return GuideErrors.InvalidOrder;
        }

        var article = new GuideArticle(category, order);
        var update = article.UpdateContent(title, body, reference, isPublished);
        return update.IsFailure ? update.Error! : article;
    }

    public Result UpdateContent(string title, string body, string? reference, bool isPublished)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(GuideErrors.TitleRequired);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure(GuideErrors.BodyRequired);
        }

        Title = title.Trim();
        Body = body.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        IsPublished = isPublished;
        return Result.Success();
    }

    public void Unpublish() => IsPublished = false;
}
