using Amazon.S3;
using Amazon.S3.Model;
using Labaik.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Labaik.Infrastructure.Storage;

internal sealed class S3FileStorage(IOptions<StorageSettings> options, TimeProvider timeProvider) : IFileStorage
{
    private readonly StorageSettings settings = options.Value;

    public async Task<string> UploadAsync(
        Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceUrl) || string.IsNullOrWhiteSpace(settings.Bucket)
            || string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey)
            || string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
        {
            throw new InvalidOperationException("Object storage is not configured.");
        }

        var now = timeProvider.GetUtcNow();
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension}";
        var config = new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = true };
        using var client = new AmazonS3Client(settings.AccessKey, settings.SecretKey, config);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, cancellationToken);

        var encodedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return $"{settings.PublicBaseUrl.TrimEnd('/')}/{encodedKey}";
    }
}
