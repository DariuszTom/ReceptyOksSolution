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


// SQLite - baza w dedykowanym folderze danych (poza folderem aplikacji dla persystencji przy deploymencie)
// W produkcji (Docker) u¿ywamy /data jako wolumenu; lokalnie u¿ywamy folderu Data w aplikacji
var dataFolder = builder.Configuration["Database:DataFolder"]
    ?? (builder.Environment.IsProduction() ? "/data" : Path.Combine(builder.Environment.ContentRootPath, "Data"));
Directory.CreateDirectory(dataFolder);
var dbName = builder.Configuration["Database:Name"] ?? "recipes.db";
var dbPath = Path.Combine(dataFolder, dbName);

// SQLite connection string z opcjami dla Azure File Share (SMB):
// - Mode=ReadWriteCreate: automatyczne tworzenie bazy
// - Cache=Shared: wspó³dzielony cache dla lepszej wydajnoœci
// - Pooling=True: connection pooling
// - Journal Mode=WAL wy³¹czony przez brak wsparcia SMB dla blokad - u¿ywamy DELETE mode
var sqliteConnectionString = builder.Environment.IsProduction()
    ? $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared"
    : $"Data Source={dbPath}";

builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));

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

    // Dla Azure File Share (SMB) wy³¹cz WAL mode - nie dzia³a z sieciowym FS
    if (app.Environment.IsProduction())
    {
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
        db.Database.ExecuteSqlRaw("PRAGMA busy_timeout=30000;");
    }

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
