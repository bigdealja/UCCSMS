using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SMS.Application.Documents;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure.Documents;

/// <summary>Document management: upload (new doc or new version), list, open, delete.
/// Metadata + versions live in the DB; bytes live in object storage. SHA-256 is recorded
/// per version for integrity.</summary>
public class DocumentService
{
    private readonly SmsDbContext _db;
    private readonly IDocumentStorage _storage;
    public DocumentService(SmsDbContext db, IDocumentStorage storage) { _db = db; _storage = storage; }

    public async Task<Document> UploadNewAsync(string title, DocumentCategory category, int? studentId,
        string? description, string fileName, string contentType, Stream content, string? user, CancellationToken ct = default)
    {
        var doc = new Document { Title = title, Category = category, StudentId = studentId, Description = description };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);                 // get doc.Id
        await AddVersionAsync(doc.Id, fileName, contentType, content, user, ct);
        return doc;
    }

    public async Task<DocumentVersion> AddVersionAsync(int documentId, string fileName, string contentType,
        Stream content, string? user, CancellationToken ct = default)
    {
        var doc = await _db.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId, ct)
                  ?? throw new InvalidOperationException($"Document {documentId} not found.");

        // Buffer once so we can hash and store the same bytes.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct)).ToLowerInvariant();
        buffer.Position = 0;

        var next = doc.CurrentVersion + 1;
        var key = $"documents/{documentId}/v{next}-{Guid.NewGuid():N}/{fileName}";
        await _storage.SaveAsync(key, buffer, contentType, ct);

        var version = new DocumentVersion
        {
            DocumentId = documentId, VersionNumber = next, StorageKey = key, FileName = fileName,
            ContentType = contentType, SizeBytes = buffer.Length, Sha256 = sha, UploadedBy = user
        };
        _db.DocumentVersions.Add(version);
        doc.CurrentVersion = next;
        await _db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<(DocumentVersion meta, Stream content)> OpenAsync(int documentId, int? version, CancellationToken ct = default)
    {
        var v = await _db.DocumentVersions.AsNoTracking()
            .Where(x => x.DocumentId == documentId && (version == null || x.VersionNumber == version))
            .OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Document or version not found.");
        var stream = await _storage.OpenAsync(v.StorageKey, ct);
        return (v, stream);
    }

    public async Task DeleteAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return;
        _db.Documents.Remove(doc);   // soft-delete (metadata retained); blobs can be GC'd later
        await _db.SaveChangesAsync(ct);
    }
}
