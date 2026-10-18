using System.Globalization;
using System.Text.Json;

namespace Labaik.Api.OpenApi;

// Typed request/response examples in the OpenAPI document (Scalar shows them), so the web dashboard
// and the Flutter app can code against realistic payloads. Examples are the real DTO/command types,
// serialized like the API serializes them (camelCase, enums as integers).
public static class OpenApiExampleExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RouteHandlerBuilder WithRequestExample(this RouteHandlerBuilder builder, object example)
        => builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            foreach (var media in operation.RequestBody?.Content?.Values ?? [])
            {
                media.Example = JsonSerializer.SerializeToNode(example, example.GetType(), Json);
            }

            return Task.CompletedTask;
        });

    // Declares the success response type (so its schema is documented) and attaches an example.
    public static RouteHandlerBuilder WithResponseExample<T>(this RouteHandlerBuilder builder, int statusCode, T example)
        => builder
            .Produces<T>(statusCode)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                var key = statusCode.ToString(CultureInfo.InvariantCulture);
                if (operation.Responses?.TryGetValue(key, out var response) == true)
                {
                    foreach (var media in response.Content?.Values ?? [])
                    {
                        media.Example = JsonSerializer.SerializeToNode(example, Json);
                    }
                }

                return Task.CompletedTask;
            });
}
