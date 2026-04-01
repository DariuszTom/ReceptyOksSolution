using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;

namespace ReceptyOks.Api.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Konfiguruje bazę danych w zależności od środowiska.
    /// Development: SQLite (z appsettings.json)
    /// Production: Azure SQL Server (z Key Vault/zmiennych środowiskowych)
    /// </summary>
    public static IServiceCollection AddRecipeDatabase(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        string connectionString;

        if (environment.IsDevelopment())
        {
            // SQLite - baza w folderze Data aplikacji
            var dataFolder = Path.Combine(environment.ContentRootPath, "Data");
            Directory.CreateDirectory(dataFolder);
            var dbName = configuration["Database:Name"] ?? "recipes.db";
            var dbPath = Path.Combine(dataFolder, dbName);
            connectionString = $"Data Source={dbPath}";

            services.AddDbContext<RecipeDbContext>(options =>
                options.UseSqlite(connectionString)
            );
        }
        else
        {
            // Production: użyj SQL Server z Key Vault (załadowane przez SecretsResolver)
            connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "SQL Server connection string 'DefaultConnection' is not configured for Production. " +
                    "Ensure Key Vault is properly configured and contains 'ConnectionStrings--DefaultConnection' secret.");
            }

            services.AddDbContext<RecipeDbContext>(options =>
                options.UseSqlServer(connectionString, opts =>
                    opts.CommandTimeout(120))
            );
        }

        return services;
    }

    /// <summary>
    /// Automatycznie tworzy/migruje bazę danych przy starcie aplikacji.
    /// </summary>
    public static IApplicationBuilder EnsureDatabaseCreated(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.EnsureCreated();

        return app;
  }
}
