using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using SMS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Student Management System API", Version = "v1" });
});

// --- Infrastructure (SQL Server via EF Core) ---
builder.Services.AddInfrastructure(builder.Configuration);

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

// --- CORS for the React SPA served from the reverse proxy ---
var spaOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(spaOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow })).AllowAnonymous();

// In Development, create/seed the database so the UI has data. Tolerant of a missing DB
// (the app still starts; endpoints that need the DB will report the error).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SMS.Infrastructure.Persistence.SmsDbContext>();
        await SMS.Infrastructure.Persistence.SeedData.EnsureSeededAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Dev seeding skipped (database not reachable).");
    }
}

app.Run();
