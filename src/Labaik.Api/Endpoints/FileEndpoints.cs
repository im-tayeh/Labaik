using Asp.Versioning;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Common.Interfaces;

namespace Labaik.Api.Endpoints;

public sealed record FileUploadDto(string Url);

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();
        var group = app.MapGroup("/api/v{version:apiVersion}/files")
            .WithApiVersionSet(versionSet).WithTags("Files").RequireAuthorization();

        group.MapPost("/", async (IFormFile? file, IFileStorage storage, HttpRequest request, CancellationToken ct) =>
        {
            if (file is null) return Labaik.Domain.Common.Results.Error.Validation("File.UnsupportedType", "A file is required.").ToProblem();

            var (extension, maxLength) = file.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ("jpg", 5L << 20),
                "image/png" => ("png", 5L << 20),
                "image/webp" => ("webp", 5L << 20),
                "application/pdf" => ("pdf", 10L << 20),
                _ => ((string?)null, 0L)
            };
            if (extension is null)
                return Labaik.Domain.Common.Results.Error.Validation("File.UnsupportedType", "Only jpg, png, webp or pdf files are supported.").ToProblem();
            if (file.Length == 0 || file.Length > maxLength)
                return Labaik.Domain.Common.Results.Error.Validation("File.TooLarge", "The file is empty or exceeds the size limit.").ToProblem();

            await using var content = new MemoryStream((int)file.Length);
            await file.CopyToAsync(content, ct);
            if (!HasExpectedSignature(content.GetBuffer().AsSpan(0, (int)content.Length), file.ContentType))
                return Labaik.Domain.Common.Results.Error.Validation("File.UnsupportedType", "The file contents do not match the declared type.").ToProblem();

            content.Position = 0;
            var url = await storage.UploadAsync(content, file.ContentType, extension, ct);
            return Results.Ok(new FileUploadDto(AbsoluteUrl(url, request)));
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithName("UploadFile")
        .WithSummary("Upload an image (5 MB) or PDF (10 MB) and return its public URL")
        .Returns<FileUploadDto>().Fails(400, 401).Validates();

        // Files kept in the database (no object storage configured). Public like object-storage URLs:
        // images load in <img>/Image.network without a token, and the id is an unguessable GUID.
        group.MapGet("/{id:guid}", async (Guid id, IFileStorage storage, HttpResponse response, CancellationToken ct) =>
        {
            var file = await storage.ReadAsync(id, ct);
            if (file is null) return Results.NotFound();
            response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.File(file.Content, file.ContentType);
        })
        .AllowAnonymous()
        .WithName("GetStoredFile")
        .ExcludeFromDescription(); // binary download behind the URL UploadFile returns, not part of the JSON contract

        return app;
    }

    // Database storage returns a path; the client needs a full URL. Behind Render the request arrives as
    // http (TLS ends at the proxy), so anything that is not a local run is https.
    private static string AbsoluteUrl(string url, HttpRequest request)
    {
        if (!url.StartsWith('/')) return url;
        var host = request.Host.Host;
        var scheme = host is "localhost" or "127.0.0.1" ? request.Scheme : Uri.UriSchemeHttps;
        return $"{scheme}://{request.Host}{url}";
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> bytes, string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        "application/pdf" => bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8),
        _ => false
    };
}
