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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure HomeSeeker tables are created");
            throw;
        }

        return app;
    }

    /// <summary>
    /// Registers all HomeSeeker services.
    /// </summary>
    public static IServiceCollection AddHomeSeekerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<HomeSeekerOptions>(configuration.GetSection(HomeSeekerOptions.SectionName));

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

        // Scan trigger queue (singleton channel for on-demand scans)
        services.AddSingleton<ScanTriggerQueue>();

        // Background service
        services.AddHostedService<HomeSeekerScanService>();

        return services;
    }
}
