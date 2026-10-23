using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Labaik.Api.IntegrationTests.Infrastructure;

namespace Labaik.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FileTests(LabaikApiFactory factory)
{
    // The endpoint only checks the PNG signature, so a few bytes after it are enough.
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3, 4];

    [Fact]
    public async Task Without_object_storage_uploads_are_kept_in_the_database_and_served_publicly()
    {
        var user = await TestUsers.CreateAsync(factory);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "logo.png");

        var upload = await user.Client.PostAsync("/api/v1/files", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var url = (await upload.Content.ReadFromJsonAsync<UploadResult>())!.Url;
        Assert.Matches("^https?://[^/]+/api/v1/files/[0-9a-f-]{36}$", url);

        // no token needed (images load straight from the URL)
        var download = await factory.CreateClient().GetAsync(new Uri(url).PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/v1/files/{Guid.NewGuid()}")).StatusCode);
    }

    private sealed record UploadResult(string Url);
}
