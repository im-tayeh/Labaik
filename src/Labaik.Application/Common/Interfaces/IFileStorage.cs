namespace Labaik.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream content, string contentType, string extension, CancellationToken cancellationToken = default);

    // Files kept by the API itself (database storage) are served back through GET /api/v1/files/{id};
    // object storage serves its own public URLs, so it has nothing to read here.
    Task<StoredFileContent?> ReadAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<StoredFileContent?>(null);
}

public sealed record StoredFileContent(byte[] Content, string ContentType);
