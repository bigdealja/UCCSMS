using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMS.Application.GradeImport;
using SMS.Application.Moodle;
using SMS.Infrastructure.GradeImport;
using SMS.Infrastructure.Moodle;
using SMS.Infrastructure.Exams;
using SMS.Application.Documents;
using SMS.Infrastructure.Documents;
using SMS.Infrastructure.Persistence;

namespace SMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Connection string comes from env/secret file (SMS_DB), NEVER from source.
        var cs = config.GetConnectionString("Sms")
                 ?? Environment.GetEnvironmentVariable("SMS_DB")
                 ?? throw new InvalidOperationException("No SMS database connection string configured.");
        services.AddDbContext<SmsDbContext>(o => o.UseSqlServer(cs));

        services.AddScoped<IGradeFileParser, SpreadsheetGradeFileParser>();
        services.AddScoped<GradeImportService>();

        // Moodle integration
        services.Configure<MoodleOptions>(config.GetSection("Moodle"));
        services.AddHttpClient<IMoodleClient, MoodleClient>();
        services.AddScoped<MoodleSyncService>();
        services.AddScoped<ExamService>();

        services.Configure<StorageOptions>(config.GetSection("Storage"));
        var provider = config.GetSection("Storage")["Provider"] ?? "FileSystem";
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IDocumentStorage, S3DocumentStorage>();
        else
            services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddScoped<DocumentService>();
        return services;
    }
}
