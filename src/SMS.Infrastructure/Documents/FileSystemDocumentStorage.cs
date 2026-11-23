using Microsoft.Extensions.Options;
using SMS.Application.Documents;

namespace SMS.Infrastructure.Documents;

/// <summary>Filesystem storage for dev and tests. Keys map to files under a root directory.</summary>
public class FileSystemDocumentStorage : IDocumentStorage
{
    private readonly string _root;
    public FileSystemDocumentStorage(IOptions<StorageOptions> opt)
        => _root = Path.GetFullPath(opt.Value.FileSystemRoot);

    string PathFor(string key) => Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenAsync(string key, CancellationToken ct = default)
        => Task.FromResult<Stream>(File.OpenRead(PathFor(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
