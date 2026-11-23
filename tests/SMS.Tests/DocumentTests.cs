using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMS.Application.Documents;
using SMS.Domain.Enums;
using SMS.Infrastructure.Documents;
using SMS.Infrastructure.Persistence;
using Xunit;

namespace SMS.Tests;

public class DocumentTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "sms-doc-tests-" + Guid.NewGuid().ToString("N"));

    SmsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<SmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new SmsDbContext(opts);
    }
    DocumentService NewService(SmsDbContext db)
    {
        var storage = new FileSystemDocumentStorage(Options.Create(new StorageOptions { FileSystemRoot = _tmp }));
        return new DocumentService(db, storage);
    }
    static Stream Bytes(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task Upload_creates_version_1_with_hash_and_size()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var doc = await svc.UploadNewAsync("Transcript", DocumentCategory.Transcript, 1, null,
            "t.txt", "text/plain", Bytes("hello"), "tester");

        Assert.Equal(1, doc.CurrentVersion);
        var v = await db.DocumentVersions.FirstAsync();
        Assert.Equal(5, v.SizeBytes);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", v.Sha256); // sha256("hello")
    }

    [Fact]
    public async Task Adding_version_increments_and_keeps_history()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var doc = await svc.UploadNewAsync("Letter", DocumentCategory.Letter, null, null, "a.txt", "text/plain", Bytes("v1"), "u");
        await svc.AddVersionAsync(doc.Id, "a.txt", "text/plain", Bytes("v2-longer"), "u");

        var reloaded = await db.Documents.FindAsync(doc.Id);
        Assert.Equal(2, reloaded!.CurrentVersion);
        Assert.Equal(2, await db.DocumentVersions.CountAsync(x => x.DocumentId == doc.Id));
    }

    [Fact]
    public async Task Download_returns_stored_bytes_for_requested_version()
    {
        using var db = NewDb();
        var svc = NewService(db);
        var doc = await svc.UploadNewAsync("Doc", DocumentCategory.Other, null, null, "a.txt", "text/plain", Bytes("first"), "u");
        await svc.AddVersionAsync(doc.Id, "a.txt", "text/plain", Bytes("second"), "u");

        var (meta1, s1) = await svc.OpenAsync(doc.Id, 1);
        using var r1 = new StreamReader(s1);
        Assert.Equal("first", await r1.ReadToEndAsync());

        var (metaLatest, s2) = await svc.OpenAsync(doc.Id, null);  // latest
        using var r2 = new StreamReader(s2);
        Assert.Equal("second", await r2.ReadToEndAsync());
        Assert.Equal(2, metaLatest.VersionNumber);
    }

    public void Dispose() { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); }
}
