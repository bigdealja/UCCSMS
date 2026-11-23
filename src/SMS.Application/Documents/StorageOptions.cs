namespace SMS.Application.Documents;

public class StorageOptions
{
    public string Provider { get; set; } = "FileSystem";   // "FileSystem" | "S3"
    public string FileSystemRoot { get; set; } = "./_documents";
    public S3Options S3 { get; set; } = new();
}

public class S3Options
{
    public string Endpoint { get; set; } = "";     // e.g. http://storage:9000 (MinIO)
    public string Bucket { get; set; } = "documents";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool ForcePathStyle { get; set; } = true; // required for MinIO
}
