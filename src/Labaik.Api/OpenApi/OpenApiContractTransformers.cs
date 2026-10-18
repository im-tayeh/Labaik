using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Labaik.Api.OpenApi;

// Makes /openapi/v1.json usable by client generators (openapi-generator -> Dart):
// - enums: integer schemas listing their values, with names in x-enum-varnames and a readable description;
// - numbers: plain integer/number types (the serializer also accepts numeric strings, which would
//   otherwise surface as "integer | string" and generate untyped fields);
// - errors: ProblemDetails documents the "code" and "traceId" extension members, and every operation
//   gets a "default" error response pointing at it.
public static class OpenApiContractTransformers
{
    public static OpenApiOptions AddContractTransformers(this OpenApiOptions options)
    {
        // 3.0 ("nullable: true") is what Dart generators handle best; 3.1 type arrays are still patchy there.
        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
        options.AddSchemaTransformer(DescribeEnums);
        options.AddSchemaTransformer(StrictNumbers);
        options.AddSchemaTransformer(DescribeProblemDetails);
        options.AddOperationTransformer(AddDefaultErrorResponse);
        options.AddDocumentTransformer(SimplifyNullableReferences);
        return options;
    }

    // "oneOf: [null, $ref]" (how nullable enums/DTOs are emitted) -> "nullable: true, allOf: [$ref]",
    // the 3.0 idiom generators turn into a plain nullable field instead of a wrapper class.
    private static Task SimplifyNullableReferences(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        var visited = new HashSet<OpenApiSchema>(ReferenceEqualityComparer.Instance);

        void Visit(IOpenApiSchema? candidate)
        {
            if (candidate is not OpenApiSchema schema || !visited.Add(schema))
            {
                return;
            }

            if (schema.OneOf is { Count: 2 } oneOf
                && oneOf.SingleOrDefault(s => s is OpenApiSchema { Type: JsonSchemaType.Null }) is { } nullBranch)
            {
                var target = oneOf.Single(s => !ReferenceEquals(s, nullBranch));
                schema.OneOf = null;
                schema.AllOf = [target];
                schema.Type = JsonSchemaType.Null; // written as "nullable: true" in 3.0
                schema.Enum = null;
            }

            foreach (var child in schema.Properties?.Values ?? []) Visit(child);
            foreach (var child in schema.AllOf ?? []) Visit(child);
            foreach (var child in schema.OneOf ?? []) Visit(child);
            foreach (var child in schema.AnyOf ?? []) Visit(child);
            Visit(schema.Items);
            Visit(schema.AdditionalProperties);
        }

        foreach (var schema in document.Components?.Schemas?.Values ?? []) Visit(schema);
        return Task.CompletedTask;
    }

    private static Task DescribeEnums(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (!type.IsEnum)
        {
            return Task.CompletedTask;
        }

        var names = Enum.GetNames(type);
        var values = names.Select(n => Convert.ToInt32(Enum.Parse(type, n))).ToArray();

        schema.Type = JsonSchemaType.Integer;
        schema.Format = "int32";
        schema.Pattern = null;
        schema.Enum = values.Select(v => (JsonNode)JsonValue.Create(v)).ToList();
        schema.Description = string.Join(", ", names.Select((n, i) => $"{values[i]} = {n}"));
        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-enum-varnames"] = new JsonNodeExtension(new JsonArray(names.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()));
        return Task.CompletedTask;
    }

    private static Task StrictNumbers(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (schema.Type is { } t && (t.HasFlag(JsonSchemaType.Integer) || t.HasFlag(JsonSchemaType.Number)) && t.HasFlag(JsonSchemaType.String))
        {
            schema.Type = t & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }

    private static Task DescribeProblemDetails(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type;
        if (context.JsonPropertyInfo is not null || !typeof(ProblemDetails).IsAssignableFrom(type))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Correlates the response with server logs; include it in bug reports."
        };

        if (type == typeof(ProblemDetails))
        {
            schema.Description = "RFC 9457 error. Business errors carry a stable machine-readable \"code\" (Area.Reason, "
                + "e.g. \"Group.AlreadyMember\"); branch on it, not on \"title\" (English, for logs).";
            schema.Properties["code"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = "Stable error code such as \"Auth.InvalidCredentials\". Absent on framework 401/403, 422 and 500."
            };
        }
        else
        {
            schema.Description = "422 validation error. \"errors\" maps camelCase JSON paths of the request "
                + "(\"fullName\", \"arrival.entryPoint\", \"stays[0].placeName\") to messages.";
        }

        return Task.CompletedTask;
    }

    private static async Task AddDefaultErrorResponse(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey("default"))
        {
            return;
        }

        var document = context.Document!;
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        if (!document.Components.Schemas.ContainsKey(nameof(ProblemDetails)))
        {
            document.Components.Schemas[nameof(ProblemDetails)] = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct);
        }

        var problem = new OpenApiSchemaReference(nameof(ProblemDetails), document);
        operation.Responses["default"] = new OpenApiResponse
        {
            Description = "Error: RFC 9457 ProblemDetails with \"code\" (see components.schemas.ProblemDetails).",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = problem }
            }
        };
    }
}
