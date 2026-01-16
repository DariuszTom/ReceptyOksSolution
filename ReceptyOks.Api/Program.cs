using Azure.Identity;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Data;
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

var app = builder.Build();

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
app.MapRecipeEndpoints();
app.MapCategoryEndpoints();
app.MapIngredientEndpoints();
app.MapSyncEndpoints();

app.Run();
