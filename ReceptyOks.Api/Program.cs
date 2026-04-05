using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using ReceptyOks.Api.Endpoints;
using ReceptyOks.Api.Extensions;
using ReceptyOks.Api.Validators;
using ReceptyOks.Shared.Configuration;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

new SecretsResolver(builder).ResolveSecrets();

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// Register SecretStore
builder.Services.AddSingleton<SecretStore>();

// Per-IP rate limiter for API key auth middleware
builder.Services.AddSingleton(PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(remoteIp, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 60,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    });
}));

// Configure JSON serialization to avoid cycles when returning EF entities with navigation properties
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Response compression (Brotli + Gzip) - significantly reduces sync payload size
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest; // Balance speed vs compression
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Konfiguracja bazy danych
builder.Services.AddRecipeDatabase(builder.Environment, builder.Configuration);

// Database health check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RecipeDbContext>("database", tags: ["ready"]);

// OpenAPI
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });
    options.AddFixedWindowLimiter("strict", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it in environment variables or user secrets.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters long (256 bits).");

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddSingleton(new CleanupOptions());

builder.Services.AddHostedService<ShoppingListCleaner>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SyncRequestValidator>();

// Register sync services
builder.Services.AddScoped<ISyncRepository, SyncRepository>();
builder.Services.AddScoped<ISyncService, SyncService>();

var app = builder.Build();
app.UseResponseCompression(); // Must be early in pipeline, before other middleware
app.UseRateLimiter();
app.UseAuthentication();
// Use API key auth middleware for all endpoints
app.UseApiKeyAuth();

// Automatyczne tworzenie/migracja bazy danych
app.EnsureDatabaseCreated();

// Aspire health checks etc.
app.MapDefaultEndpoints();

// Scalar UI w development (nowoczesna alternatywa dla Swagger UI w .NET 10)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "ReceptyOks API";
        options.Theme = ScalarTheme.BluePlanet;
    });
}

// Mapowanie endpointów
// Map authentication endpoints (ensure /api/auth/validate is available)
app.MapAuthEndpoints();
app.MapRecipeEndpoints();
app.MapCategoryEndpoints();
app.MapIngredientEndpoints();
app.MapShoppingListEndpoints();
app.MapSyncEndpoints();
app.MapTokenProviderEndpoints();
app.Run();
