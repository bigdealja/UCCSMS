using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SMS.Infrastructure;

// Bootstrap logger: captures anything that fails before the host is built.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

// --- File-based secrets (Docker / Kubernetes secrets) ---
// Any file mounted under /run/secrets overrides configuration: the file name is the config key
// (use "__" to nest, e.g. a file "ConnectionStrings__Sms" sets ConnectionStrings:Sms; "Auth__Audience"
// sets Auth:Audience). This keeps real secrets out of the environment and out of source, and works
// with Docker secrets, Kubernetes secrets, and any orchestrator that projects secrets as files.
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true, reloadOnChange: false);

// --- Structured logging (Serilog) ---
// Console sink; JSON in Production for log shippers, human-readable in Development.
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("service", "sms-api");
    if (ctx.HostingEnvironment.IsProduction())
        cfg.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
    else
        cfg.WriteTo.Console();
});

// Behind the nginx TLS terminator: trust X-Forwarded-For/Proto so the app sees the real
// client IP and the https scheme (correct redirects, secure cookies, OIDC). The proxy runs on
// the trusted internal compose network, so we accept the forwarded headers from it.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Student Management System API", Version = "v1" });
});

// --- Health checks: liveness (process up) + readiness (DB reachable) ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SMS.Infrastructure.Persistence.SmsDbContext>("database", tags: new[] { "ready" });

// --- OpenTelemetry: traces + metrics over OTLP (provider-agnostic) ---
// Only wired when an OTLP endpoint is configured, so it stays off until you point it at a
// collector (Jaeger/Tempo/Prometheus/Grafana/any OTLP backend) via OTEL_EXPORTER_OTLP_ENDPOINT.
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                   ?? builder.Configuration["Otel:Endpoint"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    var resource = ResourceBuilder.CreateDefault()
        .AddService(serviceName: "sms-api",
                    serviceVersion: System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("sms-api"))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation(o => o.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());
}

// --- Infrastructure (SQL Server via EF Core) ---
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SMS.Infrastructure.Audit.ICurrentUserAccessor, SMS.Api.Auth.HttpCurrentUserAccessor>();

// --- AuthN: OIDC/JWT from self-hosted Keycloak (shared with Moodle for SSO) ---
var authority = builder.Configuration["Auth:Authority"]; // e.g. https://auth.your-institution.edu/realms/sms
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(authority))
{
    // DEV ONLY: no Keycloak configured -> auto-authenticate so the UI works out of the box.
    builder.Services.AddAuthentication(SMS.Api.Auth.DevAuthHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, SMS.Api.Auth.DevAuthHandler>(
            SMS.Api.Auth.DevAuthHandler.SchemeName, _ => { });
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = authority;
            o.Audience = builder.Configuration["Auth:Audience"] ?? "sms-api";
            o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            o.TokenValidationParameters.ValidateIssuer = !string.IsNullOrEmpty(authority);
        });
}
builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, SMS.Api.Auth.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SMS.Api.Auth.PermissionHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, SMS.Api.Auth.PermissionClaimsTransformation>();

// --- CORS for the React SPA served from the reverse proxy ---
var spaOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(spaOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseForwardedHeaders();         // must run before anything that reads scheme/remote IP
app.UseSerilogRequestLogging();   // one tidy log line per request, with timing
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Kubernetes/compose-style probes.
// /health        -> simple summary (back-compat)
// /health/live   -> liveness: the process is up (no dependency checks)
// /health/ready  -> readiness: dependencies (database) are reachable
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow })).AllowAnonymous();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false   // no checks -> healthy whenever the app can answer
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("ready")
}).AllowAnonymous();

// In Development, create/seed the database so the UI has data. Tolerant of a missing DB
// (the app still starts; endpoints that need the DB will report the error).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SMS.Infrastructure.Persistence.SmsDbContext>();
        await SMS.Infrastructure.Persistence.SeedData.EnsureSeededAsync(db);
        await SMS.Infrastructure.Grading.GradeSchemeSeeder.EnsureSeededAsync(db);
        await SMS.Infrastructure.Cases.CaseTypeSeeder.EnsureSeededAsync(db);
        await SMS.Infrastructure.Assistant.KnowledgeSeeder.EnsureSeededAsync(db);
        await SMS.Infrastructure.Security.RoleSeeder.EnsureSeededAsync(db);
        // a demo case so the Cases board isn't empty
        if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Cases))
        {
            var ct0 = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.CaseTypes);
            db.Cases.Add(new SMS.Domain.Entities.Case { CaseNumber = ct0.Code + "-2026-0001", CaseTypeId = ct0.Id,
                Title = "Student query about grade", AssignedTo = "officer.jane",
                DueUtc = DateTimeOffset.UtcNow.AddDays(-1), Status = SMS.Domain.Enums.CaseStatus.Open });
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Dev seeding skipped (database not reachable).");
    }
}

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "SMS API terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program { }
