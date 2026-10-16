using Labaik.Domain.Common.Results;

namespace Labaik.Domain.Guides;

public static class GuideErrors
{
    public static Error InvalidOrder =>
        Error.Validation("Guide.InvalidOrder", "Article order must be greater than zero.");

    public static Error TitleRequired =>
        Error.Validation("Guide.TitleRequired", "Article title is required.");

    public static Error BodyRequired =>
        Error.Validation("Guide.BodyRequired", "Article body is required.");

    public static Error NotFound =>
        Error.NotFound("Guide.NotFound", "The guide article was not found.");
}
