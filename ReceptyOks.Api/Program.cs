using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Data;
using ReceptyOks.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// SQLite - baza w folderze Data aplikacji
var dataFolder = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataFolder);
var dbPath = Path.Combine(dataFolder, "recipes.db");

builder.Services.AddDbContext<RecipeDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// OpenAPI/Swagger
builder.Services.AddOpenApi();

var app = builder.Build();

// Automatyczne tworzenie/migracja bazy danych
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
    db.Database.EnsureCreated();
}

// Aspire health checks etc.
app.MapDefaultEndpoints();

// Swagger UI w development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ReceptyOks API v1");
    });
}

// Mapowanie endpointów
app.MapRecipeEndpoints();
app.MapCategoryEndpoints();
app.MapIngredientEndpoints();
app.MapSyncEndpoints();

app.Run();
