using Microsoft.EntityFrameworkCore;
using ReceptyOks.Api.Middleware;

namespace ReceptyOks.Api.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Konfiguruje bazę danych w zależności od środowiska.
    /// Development: SQLite
    /// Production: Azure SQL Server
    /// </summary>
    public static IServiceCollection AddRecipeDatabase(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        if (environment.IsDevelopment())
        {
            // SQLite - lokalna baza danych dla development
            services.AddDbContext<RecipeDbContext>(options =>
                options.UseSqlite(connectionString)
            );
        }
        else
        {
            // Azure SQL Database - persystentna baza w chmurze dla production
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
