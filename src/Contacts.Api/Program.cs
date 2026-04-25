using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Contacts.Application;
using Contacts.Infrastructure;
using Contacts.Infrastructure.Persistence;
using Contacts.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog structured logging ────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/contacts-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

// ── Application & Infrastructure ─────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── API Controllers ──────────────────────────────────────────────────────────
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ── API Versioning ───────────────────────────────────────────────────────────
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Api-Version"));
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// ── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Contacts API",
        Version = "v1",
        Description = "RESTful Contacts API — built following industry-standard engineering practices.",
        Contact = new OpenApiContact { Name = "Engineering Team", Email = "api-team@example.com" }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health Checks ───────────────────────────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "database",
        tags: ["db", "postgres"]);

// ── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfigured", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Rate Limiting (ASP.NET 8 built-in) ───────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ── Migrate database on startup ───────────────────────────────────────────────
// Skipped in Testing environment — migrations are run by ApiWebApplicationFactory
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware pipeline (order matters) ───────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Contacts API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger at root
    });
}

app.UseSerilogRequestLogging(opts =>
    opts.EnrichDiagnosticContext = (diagnostics, httpContext) =>
    {
        diagnostics.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress);
        diagnostics.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (correlationId is not null) diagnostics.Set("CorrelationId", correlationId);
    });

app.UseHttpsRedirection();
app.UseCors("AllowConfigured");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Always alive if the process can respond
});

await app.RunAsync();

// Needed for integration test WebApplicationFactory
public partial class Program { }
