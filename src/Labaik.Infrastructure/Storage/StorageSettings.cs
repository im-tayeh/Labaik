namespace Labaik.Infrastructure.Storage;

public sealed class StorageSettings
{
    public const string SectionName = "Storage";
    public string ServiceUrl { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string PublicBaseUrl { get; init; } = string.Empty;
}
