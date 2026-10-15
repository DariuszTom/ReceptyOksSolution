using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Evaluation;
using HomeSeeker.Scrapers;
using HomeSeeker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ReceptyOks.Api.DbUtility;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Api.Repositories;
using ReceptyOks.Api.Services;
using ReceptyOks.Shared;

namespace ReceptyOks.Api.Extensions;

/// <summary>
/// Extension methods for configuring HomeSeeker services.
/// </summary>
public static class HomeSeekerExtensions
{
    /// <summary>
    /// Configures HomeSeeker database using the same connection as RecipeDbContext.
    /// </summary>
    public static IServiceCollection AddHomeSeekerDatabase(
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

            services.AddDbContextPool<HomeSeekerDbContext>(options =>
                options.UseSqlite(connectionString)
            );
        }
        else
        {
            connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "SQL Server connection string 'DefaultConnection' is not configured for Production.");
            }

            services.AddDbContextPool<HomeSeekerDbContext>(options =>
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

    /// <summary>
    /// Ensures HomeSeeker tables are created without affecting other tables.
    /// Uses CreateTables() if database exists but HomeSeeker tables don't.
    /// 
    /// NOTE: Future column changes require manual SQL - project doesn't use migrations.
    /// </summary>
    public static IApplicationBuilder EnsureHomeSeekerTablesCreated(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeSeekerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<HomeSeekerDbContext>>();

        try
        {
            // Try to query SearchProfiles - if table doesn't exist, create tables
            var canConnect = db.Database.CanConnect();
            if (!canConnect)
            {
                // Fresh database - EnsureCreated will work
                db.Database.EnsureCreated();
                logger.LogInformation("HomeSeeker database created");
                return app;
            }

            // Database exists - check if our tables exist
            try
            {
                _ = db.SearchProfiles.Any();
                logger.LogDebug("HomeSeeker tables already exist");
            }
            catch (Exception)
            {
                // Tables don't exist - create just our tables
                var creator = db.GetService<IRelationalDatabaseCreator>();
                creator.CreateTables();
                logger.LogInformation("HomeSeeker tables created in existing database");
            }

            // Lightweight in-place schema upgrades. The project does not use EF
            // migrations; these guard existing DBs created before a column was added.
            EnsureColumn(db, logger, "SearchProfiles", "ScanLeaseUntil",
                sqliteType: "TEXT NULL", sqlServerType: "DATETIME2 NULL");
            EnsureColumn(db, logger, "SearchProfiles", "ScanLeaseToken",
                sqliteType: "TEXT NULL", sqlServerType: "UNIQUEIDENTIFIER NULL");
            EnsureColumn(db, logger, "SearchProfiles", "OwnerId",
                sqliteType: "TEXT NULL", sqlServerType: "NVARCHAR(200) NULL");
            // Existing profiles/listings predate apartments, so they default to House (1).
            EnsureColumn(db, logger, "SearchProfiles", "PropertyTypes",
                sqliteType: "INTEGER NOT NULL DEFAULT 1", sqlServerType: "INT NOT NULL DEFAULT 1");
            EnsureColumn(db, logger, "HouseListings", "PropertyType",
                sqliteType: "INTEGER NOT NULL DEFAULT 1", sqlServerType: "INT NOT NULL DEFAULT 1");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure HomeSeeker tables are created");
            throw;
        }

        return app;
    }

    private static void EnsureColumn(
        HomeSeekerDbContext db,
        ILogger logger,
        string table,
        string column,
        string sqliteType,
        string sqlServerType)
    {
        if (ColumnExists(db, table, column))
        {
            logger.LogDebug("{Column} column already present on {Table}", column, table);
            return;
        }

        var sql = db.Database.IsSqlite()
            ? $"ALTER TABLE {table} ADD COLUMN {column} {sqliteType};"
            : $"ALTER TABLE {table} ADD {column} {sqlServerType};";

        try
        {
            db.Database.ExecuteSqlRaw(sql);
            logger.LogInformation("Added {Column} column to {Table}", column, table);
        }
        catch (Exception ex) when (ColumnExists(db, table, column))
        {
            // Lost a race with another instance that added the column concurrently.
            logger.LogDebug(ex, "{Column} column was added concurrently by another instance", column);
        }
        // Any other failure (missing DDL rights, transient DB error) propagates so startup
        // fails fast instead of every later profile query failing at runtime.
    }

    private static bool ColumnExists(HomeSeekerDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();

            if (db.Database.IsSqlite())
            {
                command.CommandText = $"PRAGMA table_info({table});";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }

            command.CommandText =
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column;";

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@column";
            columnParam.Value = column;
            command.Parameters.Add(columnParam);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }

    /// <summary>
    /// Registers all HomeSeeker services.
    /// </summary>
    public static IServiceCollection AddHomeSeekerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration with startup validation (CODE_REVIEW.md H9).
        // ValidateOnStart ensures the host refuses to start with invalid HomeSeeker
        // options rather than failing later from a background service (e.g.
        // PeriodicTimer throwing on a non-positive ScanInterval).
        services
            .AddOptions<HomeSeekerOptions>()
            .Bind(configuration.GetSection(HomeSeekerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => o.ScanInterval > TimeSpan.Zero,
                "HomeSeeker:ScanInterval must be positive.")
            .ValidateOnStart();

        // Register named HttpClient for scrapers
        services.AddHttpClient("homeseeker-scraper", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language", "pl-PL,pl;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Scrapers
        services.AddSingleton<IListingScraper, OtodomScraper>();
        services.AddSingleton<IListingScraper, OlxScraper>();

        // Repository
        services.AddScoped<IListingRepository, ListingRepository>();

        // AI Factory
        services.AddSingleton<IAiAgentFactory, AnthropicAiAgentFactory>();

        // Evaluator
        services.AddScoped<IListingEvaluator, AgentListingEvaluator>();

        // Report sender
        services.AddSingleton<IScanReportSender, EmailScanReportSender>();

        // Market scan service
        services.AddScoped<IMarketScanService, MarketScanService>();

        // Background service
        services.AddHostedService<HomeSeekerScanService>();

        return services;
    }
}
