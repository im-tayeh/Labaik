using Asp.Versioning;
using Labaik.Api.OpenApi;

namespace Labaik.Api.Extensions;

public static class ApiVersioningExtensions
{
    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true; // adds "api-supported-versions" response header
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            // One OpenAPI document per API version (/openapi/v1.json, ...), generator-friendly.
            .AddOpenApi(options => options.Document.AddContractTransformers());

        return services;
    }
}