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
using SMS.Application.Accounting;
using SMS.Infrastructure.Accounting;
using SMS.Infrastructure.Finance;
using SMS.Infrastructure.Grading;
using SMS.Application.Crm;
using SMS.Infrastructure.Crm;
using SMS.Infrastructure.Admissions;
using SMS.Infrastructure.Cases;
using SMS.Application.Ai;
using SMS.Infrastructure.Ai;
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
        services.AddScoped<ExamSchedulingService>();

        services.Configure<StorageOptions>(config.GetSection("Storage"));
        var provider = config.GetSection("Storage")["Provider"] ?? "FileSystem";
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IDocumentStorage, S3DocumentStorage>();
        else
            services.AddScoped<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddScoped<DocumentService>();

        // Accounting integration is provider-agnostic: choose the implementation by config.
        // Add a new provider (QuickBooks, Xero, …) by implementing IAccountingProvider and
        // registering it here under its name — the finance module is untouched.
        services.Configure<AccountingOptions>(config.GetSection("Accounting"));
        var accProvider = config.GetSection("Accounting")["Provider"] ?? "None";
        if (string.Equals(accProvider, "Sage300", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IAccountingProvider, Sage300AccountingProvider>();
        else
            services.AddScoped<IAccountingProvider, NullAccountingProvider>();
        services.AddScoped<FinanceService>();
        services.AddScoped<GradingService>();

        // CRM is provider-agnostic: built-in, or an external CRM (Mautic, Salesforce, …) by config.
        services.Configure<CrmOptions>(config.GetSection("Crm"));
        var crmProvider = config.GetSection("Crm")["Provider"] ?? "BuiltIn";
        if (string.Equals(crmProvider, "Webhook", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<ICrmProvider, WebhookCrmProvider>();
        else
            services.AddScoped<ICrmProvider, BuiltInCrmProvider>();
        services.AddScoped<AdmissionsService>();

        // AI is pluggable and OFF by default (data-protection approval required to enable).
        services.Configure<AiOptions>(config.GetSection("Ai"));
        var aiProvider = config.GetSection("Ai")["Provider"] ?? "None";
        if (string.Equals(aiProvider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IAiAssistant, ChatCompletionAiAssistant>();
        else
            services.AddScoped<IAiAssistant, NullAiAssistant>();
        services.AddScoped<AdmissionsAiService>();
        services.AddScoped<CaseService>();
        return services;
    }
}
