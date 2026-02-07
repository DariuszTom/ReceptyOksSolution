using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Endpoints;
using ReceptyOks.Api.Middleware;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

new SecretsResolver(builder).ResolveSecrets();

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// Register SecretStore
builder.Services.AddSingleton<SecretStore>();

// Configure JSON serialization to avoid cycles when returning EF entities with navigation properties
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});


// SQLite - baza w folderze Data aplikacji
var dataFolder = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataFolder);
var dbName = builder.Configuration["Database:Name"] ?? "recipes.db";
var dbPath = Path.Combine(dataFolder, dbName);

builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

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
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "default_secret_key"))
        };
    });
var app = builder.Build();

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
app.UseRateLimiter();
app.Run();
