using System.Text;
using System.Security.Claims;
using System.Threading.RateLimiting;
using B2B.Contracts;
using B2B.Api.Middleware;
using B2B.Api.Security;
using Microsoft.AspNetCore.Authorization;
using B2B.Application;
using B2B.Infrastructure;
using B2B.Api.Infrastructure;
using B2B.Api.Persistence;
using B2B.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSwag.AspNetCore;
using Serilog;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    kv => string.IsNullOrEmpty(kv.Key) ? "_" : kv.Key,
                    kv => kv.Value!.Errors
                        .Select(e =>
                            string.IsNullOrWhiteSpace(e.ErrorMessage)
                                ? e.Exception?.Message ?? "Geçersiz değer."
                                : e.ErrorMessage)
                        .ToArray());

            var flat = details.Values.SelectMany(v => v).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            var message = flat.Count == 0
                ? "İstek doğrulanamadı."
                : string.Join(" ", flat);

            var traceId = context.HttpContext.TraceIdentifier;
            var payload = ApiResponse<object>.Fail(
                new ApiError("validation_failed", message, details),
                traceId);
            return new BadRequestObjectResult(payload);
        };
    });

builder.Services.AddEndpointsApiExplorer();

var enableSwagger = builder.Environment.IsDevelopment()
    || builder.Configuration.GetValue("Api:EnableSwagger", false);
if (enableSwagger)
{
    builder.Services.AddOpenApiDocument(config =>
    {
        config.Title = "B2B API";
        config.Version = "v1";
        config.Description = "Mobile-friendly B2B backend API (paginated lists, filtering, standardized envelopes).";
    });
}

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<B2BDbContext>(
        name: "sql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

// CORS (explicit, config-driven). If no origins are configured, we keep the default ASP.NET posture (same-origin).
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var enableCors = corsOrigins.Length > 0;
if (enableCors)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("default", p =>
            p.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
        );
    });
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? httpContext.Request.Headers.Host.ToString();
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Broad protection for catalog browsing and other read-heavy endpoints.
    options.AddPolicy("read", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? httpContext.Request.Headers.Host.ToString();
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Broad protection for write-heavy endpoints (orders, admin updates).
    options.AddPolicy("write", httpContext =>
    {
        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString()
            : $"user:{userId}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Stricter protection for uploads.
    options.AddPolicy("uploads", httpContext =>
    {
        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString()
            : $"user:{userId}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        var traceId = context.HttpContext.TraceIdentifier;
        var body = ApiResponse<object>.Fail(
            new ApiError(
                "rate_limited",
                "Çok fazla istek. Lütfen kısa süre sonra tekrar deneyin.",
                null),
            traceId);
        await context.HttpContext.Response.WriteAsJsonAsync(body, ct);
    };
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddOptions<PublicAssetUrlOptions>()
    .BindConfiguration(PublicAssetUrlOptions.SectionName);

builder.Services.AddOptions<ApiPublishingOptions>()
    .BindConfiguration(ApiPublishingOptions.SectionName);

builder.Services.AddOptions<UploadLimitsOptions>()
    .BindConfiguration(UploadLimitsOptions.SectionName);

builder.Services.AddOptions<ObjectStorageOptions>()
    .BindConfiguration(ObjectStorageOptions.SectionName);

builder.Services.AddOptions<B2B.Api.Push.PushOptions>()
    .BindConfiguration(B2B.Api.Push.PushOptions.SectionName);

builder.Services.AddOptions<B2B.Api.Push.FcmOptions>()
    .BindConfiguration(B2B.Api.Push.FcmOptions.SectionName);

builder.Services.AddSingleton<B2B.Api.Push.IPushSender>(sp =>
{
    var enabled = sp.GetRequiredService<IOptions<B2B.Api.Push.PushOptions>>().Value.Enabled;
    return enabled
        ? ActivatorUtilities.CreateInstance<B2B.Api.Push.FcmPushSender>(sp)
        : new B2B.Api.Push.NoopPushSender();
});

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    return S3ObjectStorage.BuildClient(o);
});

builder.Services.AddSingleton<IObjectStorage>(sp =>
{
    var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    var provider = (o.Provider ?? "Local").Trim();
    return provider.Equals("S3", StringComparison.OrdinalIgnoreCase)
        ? new S3ObjectStorage(sp.GetRequiredService<IAmazonS3>(), sp.GetRequiredService<IOptions<ObjectStorageOptions>>())
        : new LocalFileObjectStorage(
            sp.GetRequiredService<IWebHostEnvironment>(),
            sp.GetRequiredService<IOptions<ApiPublishingOptions>>(),
            sp.GetRequiredService<IOptions<ObjectStorageOptions>>());
});

// Include API-layer validators (request DTOs defined in controllers)
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);

builder.Services
    .AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();

builder.Services.AddTransient<CorrelationIdMiddleware>();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();

builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .PostConfigure(o =>
    {
        if (string.IsNullOrWhiteSpace(o.SigningKey) && !string.IsNullOrWhiteSpace(o.Key))
            o.SigningKey = o.Key;
    })
    .ValidateOnStart();

builder.Services.AddSingleton<JwtKeyMaterial>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();

builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>, JwtKeyMaterial>((options, jwtOptions, keyMaterial) =>
    {
        var jwt = jwtOptions.Value;
        var signingKey = keyMaterial.GetSigningKey();

        options.RequireHttpsMetadata = !builder.Environment.IsEnvironment("Testing");
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var traceId = context.HttpContext.TraceIdentifier;
                var body = ApiResponse<object>.Fail(
                    new ApiError(
                        "unauthorized",
                        "Oturum geçersiz veya erişim jetonunun süresi dolmuş.",
                        null),
                    traceId);
                await context.Response.WriteAsJsonAsync(body, context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AdminOnly, p => p.RequireRole("Admin"));
    options.AddPolicy(AuthorizationPolicies.DealerOnly, p => p.RequireRole("Dealer"));
});

var app = builder.Build();

// Production guardrails: fail fast if dangerous dev settings leak into non-dev environments.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    static string? GetString(IConfiguration cfg, string key) => cfg[key];
    static bool GetBool(IConfiguration cfg, string key) => cfg.GetValue<bool>(key);

    var seedMode = (GetString(app.Configuration, "Seed:Mode") ?? "Off").Trim();
    if (!seedMode.Equals("Off", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Seed:Mode must be Off outside Development/Testing.");

    if (GetBool(app.Configuration, "Database:ApplyMigrationsOnStartup"))
        throw new InvalidOperationException("Database:ApplyMigrationsOnStartup must be false outside Development/Testing.");

    // Public registration is a high-risk production setting. Allow it only when explicitly acknowledged
    // via Auth:AllowPublicRegistrationInProduction=true (e.g., for controlled rollouts).
    var allowReg = GetBool(app.Configuration, "Auth:AllowPublicRegistration");
    var allowRegInProd = GetBool(app.Configuration, "Auth:AllowPublicRegistrationInProduction");
    if (allowReg && !allowRegInProd)
        throw new InvalidOperationException(
            "Auth:AllowPublicRegistration is enabled, but Auth:AllowPublicRegistrationInProduction is not. " +
            "Set Auth:AllowPublicRegistrationInProduction=true to explicitly acknowledge the risk.");

    if (GetBool(app.Configuration, "Api:EnableSwagger"))
        throw new InvalidOperationException("Api:EnableSwagger must be false outside Development/Testing.");

    if (GetBool(app.Configuration, "PublicAssets:RewritePrivateLanUploadUrls"))
        throw new InvalidOperationException("PublicAssets:RewritePrivateLanUploadUrls must be false outside Development/Testing.");
}

if (!app.Environment.IsEnvironment("Testing"))
{
    var sqlCs = app.Configuration.GetConnectionString("SqlServer");
    if (string.IsNullOrWhiteSpace(sqlCs))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:SqlServer is not configured. For local development use User Secrets (see B2B.Api/CONFIGURATION.sample.env). " +
            "For production use environment variables, Azure Key Vault, or your host's secret injection.");
    }
}

// Apply migrations on startup (env/config controlled) + optional seed
var applyMigrations = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
if (applyMigrations || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<B2BDbContext>();
    await db.Database.MigrateAsync();

    // Controlled by Seed:Mode (Off/RolesOnly/RolesAndAdmin/Demo). Roles: Admin + Dealer (bayi).
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (enableSwagger)
{
    app.UseOpenApi(o => o.Path = "/swagger/v1/swagger.json");
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
        settings.DocumentPath = "/swagger/v1/swagger.json";
    });
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (enableCors)
{
    app.UseCors("default");
}

app.UseStaticFiles();

// Only enable HTTPS redirection when the app itself is configured to speak HTTPS.
// In IP-only / reverse-proxy-terminated setups (HTTP between proxy and app), enabling this causes noisy logs:
// "Failed to determine the https port for redirect."
if (!app.Environment.IsEnvironment("Testing"))
{
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
    var hasHttpsUrl = urls
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    var httpsPort = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT");
    var hasHttpsPort = !string.IsNullOrWhiteSpace(httpsPort);

    if (hasHttpsUrl || hasHttpsPort)
        app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

public partial class Program { }
