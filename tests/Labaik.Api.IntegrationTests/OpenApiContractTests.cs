using System.Text.Json;
using System.Text.Json.Nodes;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

// Keeps /openapi/v1.json generator-ready (openapi-generator -> Dart) and in sync with the committed
// snapshot docs/openapi/v1.json. After an intended API change, refresh the snapshot with:
//   UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiContractTests
[Collection(ApiCollection.Name)]
public class OpenApiContractTests(LabaikApiFactory factory)
{
    private static readonly string[] Methods = ["get", "post", "put", "delete", "patch"];

    private async Task<JsonObject> GetDocumentAsync()
    {
        var json = await factory.CreateClient().GetStringAsync("/openapi/v1.json");
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("servers"); // host-specific
        return document;
    }

    private static IEnumerable<(string Label, JsonObject Operation)> Operations(JsonObject document) =>
        document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject()
            .Where(m => Methods.Contains(m.Key))
            .Select(m => ($"{m.Key.ToUpperInvariant()} {path.Key}", m.Value!.AsObject())));

    [Fact]
    public async Task Every_operation_has_an_id_a_typed_success_response_and_a_problem_default()
    {
        var document = await GetDocumentAsync();
        var operations = Operations(document).ToList();
        var problems = new List<string>();

        foreach (var (label, op) in operations)
        {
            var responses = op["responses"]!.AsObject();
            var success = responses.Where(r => r.Key.StartsWith('2')).ToList();

            if (op["operationId"] is null) problems.Add($"{label}: no operationId");
            if (success.Count == 0) problems.Add($"{label}: no 2xx response");
            problems.AddRange(success
                .Where(r => r.Key != "204" && r.Value!["content"]?["application/json"]?["schema"] is null)
                .Select(r => $"{label}: {r.Key} has no JSON schema"));
            if (responses["default"]?["content"]?["application/problem+json"]?["schema"]?["$ref"]?.GetValue<string>()
                != "#/components/schemas/ProblemDetails")
            {
                problems.Add($"{label}: no ProblemDetails default response");
            }
        }

        var ids = operations.Select(o => o.Operation["operationId"]?.GetValue<string>()).ToList();
        problems.AddRange(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => $"duplicate operationId {g.Key}"));

        Assert.True(operations.Count >= 98, $"Only {operations.Count} operations documented.");
        Assert.Empty(problems);
    }

    [Fact]
    public async Task Schemas_are_generator_friendly()
    {
        var document = await GetDocumentAsync();
        var schemas = document["components"]!["schemas"]!.AsObject();
        var text = document.ToJsonString();

        Assert.Equal("3.0.4", document["openapi"]!.GetValue<string>());
        Assert.DoesNotContain("\"oneOf\"", text);       // nullable refs are "nullable + allOf"
        Assert.DoesNotContain("\"type\":[", text);      // no 3.1 type arrays
        Assert.DoesNotContain("^-?(?:0|[1-9]", text);   // numbers are not "integer | string"

        foreach (var (name, schema) in schemas.Where(s => s.Value!["enum"] is not null))
        {
            var values = schema!["enum"]!.AsArray();
            var names = schema["x-enum-varnames"]?.AsArray();
            Assert.True(names is not null && names.Count == values.Count, $"{name}: enum without x-enum-varnames");
            Assert.Equal("integer", schema["type"]!.GetValue<string>());
        }

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17],
            schemas["NotificationType"]!["enum"]!.AsArray().Select(v => v!.GetValue<int>()));
        Assert.NotNull(schemas["ProblemDetails"]!["properties"]!["code"]);
        Assert.NotNull(schemas["HttpValidationProblemDetails"]!["properties"]!["errors"]);
    }

    [Fact]
    public async Task Committed_snapshot_matches_the_generated_document()
    {
        var document = await GetDocumentAsync();
        var path = Path.Combine(RepositoryRoot(), "docs", "openapi", "v1.json");
        var generated = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, generated);
        }

        Assert.True(File.Exists(path), "docs/openapi/v1.json is missing; run with UPDATE_OPENAPI_SNAPSHOT=1.");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path));
        Assert.True(JsonNode.DeepEquals(committed, JsonNode.Parse(generated)),
            "docs/openapi/v1.json is out of date; run the tests with UPDATE_OPENAPI_SNAPSHOT=1 and commit it.");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Labaik.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Labaik.slnx) not found.");
    }
}
