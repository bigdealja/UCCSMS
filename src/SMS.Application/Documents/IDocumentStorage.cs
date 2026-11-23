namespace SMS.Application.Documents;

/// <summary>Object-storage abstraction. Production uses MinIO/S3; dev & tests use the filesystem.
/// Keeps the document bytes out of the database and the domain free of storage concerns.</summary>
public interface IDocumentStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> OpenAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
