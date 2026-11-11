using ReceptyOks.Shared;

namespace ReceptyOks.Api.Extensions;

public static class DatabaseExtensions
{
    public const string ProductionConnectionStringName = "DefaultConnection";
    public const string DevelopmentConnectionStringName = "receptyoksdb";

    /// <summary>
    /// Konfiguruje bazę danych SQL Server.
    /// Development: kontener SQL Server zarządzany przez Aspire (AppHost wstrzykuje connection string).
    /// Production: Azure SQL (connection string z Key Vault/zmiennych środowiskowych).
    /// </summary>
    public static IServiceCollection AddRecipeDatabase(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        var connectionString = GetRequiredConnectionString(environment, configuration);

        // Use pooling to reduce memory allocations (default pool size 1024)
        services.AddDbContextPool<RecipeDbContext>(options => options.UseRecipeSqlServer(connectionString));

        return services;
    }

    internal static string GetRequiredConnectionString(IWebHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment())
        {
            var developmentConnectionString = configuration.GetConnectionString(DevelopmentConnectionStringName);
            if (string.IsNullOrWhiteSpace(developmentConnectionString))
            {
                throw new InvalidOperationException(
                    $"SQL Server connection string '{DevelopmentConnectionStringName}' is not configured. " +
                    "Run the API through the Aspire AppHost (ReceptyOksSolution.AppHost), which starts the local SQL Server container.");
            }

            return developmentConnectionString;
        }

        var connectionString = configuration.GetConnectionString(ProductionConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server connection string '{ProductionConnectionStringName}' is not configured. " +
                $"Ensure Key Vault is properly configured and contains 'ConnectionStrings--{ProductionConnectionStringName}' secret.");
        }

        return connectionString;
    }

    internal static DbContextOptionsBuilder UseRecipeSqlServer(
        this DbContextOptionsBuilder options,
        string connectionString) =>
        options.UseSqlServer(connectionString, opts =>
        {
            opts.CommandTimeout(240);
            opts.EnableRetryOnFailure(
                maxRetryCount: GlobalConstants.MaxRetryAttempts,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            opts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

    public static IApplicationBuilder EnsureDatabaseCreated(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.EnsureCreated();
        return app;
    }
}
