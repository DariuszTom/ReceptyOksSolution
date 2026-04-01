using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Endpoints;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared.Configuration;
using Scalar.AspNetCore;


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


// Azure SQL Database - persystentna baza w chmurze
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlServer(connectionString, opts => 
    opts.CommandTimeout(120))
);

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
builder.Services.AddSingleton(new CleanupOptions
{
    Interval = TimeSpan.FromHours(24),
    StartupDelay = TimeSpan.FromSeconds(30),
    MaxAge = TimeSpan.FromDays(7)
});
builder.Services.AddHostedService<ShopingListCleaner>();

var app = builder.Build();
app.UseRateLimiter();
app.UseAuthentication();
// Use API key auth middleware for all endpoints
app.UseApiKeyAuth();
// Automatyczne tworzenie/migracja bazy danych
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
    db.Database.EnsureCreated();
}

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
