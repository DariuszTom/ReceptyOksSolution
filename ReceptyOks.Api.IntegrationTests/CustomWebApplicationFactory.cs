using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared;
using System.Security.Cryptography;
using System.Text;

namespace ReceptyOks.Api.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures in-memory database and test authentication.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Test API key that can be used in X-Api-Key header.
    /// </summary>
    public const string TestApiKey = "test-api-key-for-integration-tests";

    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Compute HMAC hash for the test API key
        var secretKey = "test-secret-key-for-hmac-signing-32chars!";
        var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
        using var hmac = new HMACSHA256(secretKeyBytes);
        var passwordHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(TestApiKey));
        var passwordHashBase64 = Convert.ToBase64String(passwordHashBytes);
        var secretKeyBase64 = Convert.ToBase64String(secretKeyBytes);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordHash"] = passwordHashBase64,
                ["SecretKey"] = secretKeyBase64,
                ["Jwt:Key"] = "test-jwt-key-must-be-at-least-32-chars-long!"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations (including pooling services)
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("RecipeDbContext") == true ||
                            d.ServiceType.FullName?.Contains("EntityFramework") == true &&
                            d.ServiceType.FullName?.Contains("RecipeDbContext") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Also remove by specific types
            services.RemoveAll<DbContextOptions<RecipeDbContext>>();
            services.RemoveAll<RecipeDbContext>();
            services.RemoveAll(typeof(IDbContextPool<RecipeDbContext>));
            services.RemoveAll(typeof(IScopedDbContextLease<RecipeDbContext>));

            // Add in-memory database for testing (without pooling)
            services.AddDbContext<RecipeDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });

        builder.UseEnvironment("Development");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure database is created after the host is built
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    /// <summary>
    /// Creates an HttpClient with the test API key header pre-configured.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(GlobalConstants.ApiKeyHeaderName, TestApiKey);
        return client;
    }
}
