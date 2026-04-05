using ReceptyOks.Shared;

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

            var dataFolder = Path.Combine(environment.ContentRootPath, "Data");
            Directory.CreateDirectory(dataFolder);
            var dbName = configuration["Database:Name"] ?? "recipes.db";
            var dbPath = Path.Combine(dataFolder, dbName);
            connectionString = $"Data Source={dbPath}";

            // Use pooling to reduce memory allocations
            services.AddDbContextPool<RecipeDbContext>(options =>
                options.UseSqlite(connectionString)
            );
        }
        else
        {
            connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "SQL Server connection string 'DefaultConnection' is not configured for Production. " +
                    "Ensure Key Vault is properly configured and contains 'ConnectionStrings--DefaultConnection' secret.");
            }

            // Use pooling to reduce memory allocations (default pool size 1024)
            services.AddDbContextPool<RecipeDbContext>(options =>
                options.UseSqlServer(connectionString, opts =>
                {
                    opts.CommandTimeout(240);
                    opts.EnableRetryOnFailure(
                        maxRetryCount: GlobalConstants.MaxRetryAttempts,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                    opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
            );
        }

        return services;
    }

    public static IApplicationBuilder EnsureDatabaseCreated(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.EnsureCreated();
        return app;
    }
}
