using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

// Behind Azure Container Apps ingress (Envoy) the TCP peer is the proxy, not the client.
// The container is reachable only via ingress, so trust its X-Forwarded-* headers.
// ForwardLimit = 1 takes only the right-most X-Forwarded-For entry (appended by Envoy), so client-spoofed values are ignored.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-IP rate limiter for API key auth middleware
builder.Services.AddSingleton(PartitionedRateLimiter.Create<HttpContext, string>(context =>
{
    return RateLimitPartition.GetFixedWindowLimiter(context.GetClientIp(), _ => new FixedWindowRateLimiterOptions
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

// HomeSeeker database (same connection, separate context)
builder.Services.AddHomeSeekerDatabase(builder.Environment, builder.Configuration);

// HomeSeeker services
builder.Services.AddHomeSeekerServices(builder.Configuration);

// Database health check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RecipeDbContext>("database", tags: ["ready"]);

// OpenAPI
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("fixed", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.GetClientIp(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.GetClientIp(), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
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
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton(new CleanupOptions());

builder.Services.AddHostedService<ShoppingListCleaner>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SyncRequestValidator>();

// Register sync services
builder.Services.AddScoped<ISyncRepository, SyncRepository>();
builder.Services.AddScoped<ISyncService, SyncService>();

var app = builder.Build();
app.UseForwardedHeaders(); // Must run first so RemoteIpAddress/Scheme reflect the real client
app.UseResponseCompression(); // Must be early in pipeline, before other middleware
app.UseRateLimiter();
app.UseAuthentication();
// Use API key auth middleware for all endpoints
app.UseApiKeyAuth();
// Endpoint-level authorization (only endpoints that opt in via RequireAuthorization are affected)
app.UseAuthorization();

// Automatyczne tworzenie/migracja bazy danych
app.EnsureDatabaseCreated();

// HomeSeeker tables (uses CreateTables() if database exists)
app.EnsureHomeSeekerTablesCreated();

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
app.MapHomeSeekerEndpoints();
app.Run();
