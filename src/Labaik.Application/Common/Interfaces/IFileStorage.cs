namespace Labaik.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream content, string contentType, string extension, CancellationToken cancellationToken = default);
}
