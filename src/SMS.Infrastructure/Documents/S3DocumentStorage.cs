using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using SMS.Application.Documents;

namespace SMS.Infrastructure.Documents;

/// <summary>MinIO / S3-compatible object storage for production. On-prem MinIO just needs
/// an endpoint + credentials and path-style addressing. Bytes never touch the database.</summary>
public class S3DocumentStorage : IDocumentStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3DocumentStorage(IOptions<StorageOptions> opt)
    {
        var o = opt.Value.S3;
        _bucket = o.Bucket;
        var cfg = new AmazonS3Config { ServiceURL = o.Endpoint, ForcePathStyle = o.ForcePathStyle };
        _s3 = new AmazonS3Client(o.AccessKey, o.SecretKey, cfg);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => await _s3.PutObjectAsync(new PutObjectRequest
        { BucketName = _bucket, Key = key, InputStream = content, ContentType = contentType, AutoCloseStream = false }, ct);

    public async Task<Stream> OpenAsync(string key, CancellationToken ct = default)
    {
        var resp = await _s3.GetObjectAsync(_bucket, key, ct);
        var ms = new MemoryStream();
        await resp.ResponseStream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _s3.DeleteObjectAsync(_bucket, key, ct);
}
