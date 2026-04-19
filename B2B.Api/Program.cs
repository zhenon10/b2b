using B2B.Api.Contracts;
using B2B.Api.Middleware;
using B2B.Api.Security;
using B2B.Application;
using B2B.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using FluentValidation.AspNetCore;
using Serilog;
using NSwag.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using B2B.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FluentValidation;
using B2B.Api.Infrastructure;
using B2B.Api.Persistence;

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
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "B2B API";
    config.Version = "v1";
    config.Description = "Mobile-friendly B2B backend API (paginated lists, filtering, standardized envelopes).";
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<PublicAssetUrlOptions>()
    .BindConfiguration(PublicAssetUrlOptions.SectionName);

builder.Services.AddOptions<ApiPublishingOptions>()
    .BindConfiguration(ApiPublishingOptions.SectionName);

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

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
app.UseOpenApi(o => o.Path = "/swagger/v1/swagger.json");
app.UseSwaggerUi(settings =>
{
    settings.Path = "/swagger";
    settings.DocumentPath = "/swagger/v1/swagger.json";
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseStaticFiles();

// Mobile devices on local Wi‑Fi typically can't trust dev HTTPS certs.
// Keep HTTPS redirection for non-dev environments.
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
