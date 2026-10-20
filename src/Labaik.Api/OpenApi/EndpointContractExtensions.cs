namespace Labaik.Api.OpenApi;

// Short, explicit contract declarations for OpenAPI: success type + documented error statuses.
// Every operation also gets a "default" ProblemDetails response (OpenApiContractTransformers).
public static class EndpointContractExtensions
{
    public static RouteHandlerBuilder Returns<T>(this RouteHandlerBuilder builder, int statusCode = StatusCodes.Status200OK)
        => builder.Produces<T>(statusCode);

    public static RouteHandlerBuilder ReturnsNoContent(this RouteHandlerBuilder builder)
        => builder.Produces(StatusCodes.Status204NoContent);

    // RFC 9457 ProblemDetails (with "code") for each listed status.
    public static RouteHandlerBuilder Fails(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        foreach (var statusCode in statusCodes)
        {
            builder.ProducesProblem(statusCode);
        }

        return builder;
    }

    // 422 with the camelCase "errors" dictionary.
    public static RouteHandlerBuilder Validates(this RouteHandlerBuilder builder)
        => builder.ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
}

// Named shapes for responses that used anonymous objects (same JSON as before).
public sealed record MessageResponse(string Message);

public sealed record MeResponse(string? Id, string? Email, string? Name, IReadOnlyList<string> Roles, bool MustChangePassword = false);

public sealed record CreatedIdResponse(Guid Id);
