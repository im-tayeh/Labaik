using Labaik.Application.Common.Interfaces;
using Labaik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Infrastructure.Storage;

// Fallback when no object storage (Storage:*) is configured: uploads live in PostgreSQL and the API
// serves them at /api/v1/files/{id}. Returns that path relative; FileEndpoints makes it absolute.
// The table is created on first use with raw SQL, outside the EF model, so no migration is needed
// and switching to S3 later only means setting the Storage:* values.
internal sealed class DatabaseFileStorage(AppDbContext context, TimeProvider timeProvider) : IFileStorage
{
    public const string PathPrefix = "/api/v1/files/";

    private static volatile bool tableReady;

    public async Task<string> UploadAsync(
        Stream content, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();

        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO stored_files (id, content_type, content, created_at_utc) VALUES ({id}, {contentType}, {bytes}, {now})",
            cancellationToken);

        return $"{PathPrefix}{id:D}";
    }

    public async Task<StoredFileContent?> ReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var rows = await context.Database
            .SqlQuery<StoredFileRow>($"SELECT content AS \"Content\", content_type AS \"ContentType\" FROM stored_files WHERE id = {id}")
            .ToListAsync(cancellationToken);
        var row = rows.FirstOrDefault();
        return row is null ? null : new StoredFileContent(row.Content, row.ContentType);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (tableReady) return;
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS stored_files (id uuid PRIMARY KEY, content_type text NOT NULL, content bytea NOT NULL, created_at_utc timestamptz NOT NULL)",
            cancellationToken);
        tableReady = true;
    }

    private sealed class StoredFileRow
    {
        public byte[] Content { get; init; } = [];
        public string ContentType { get; init; } = string.Empty;
    }
}
