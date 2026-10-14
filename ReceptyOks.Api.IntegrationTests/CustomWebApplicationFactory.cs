using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ReceptyOks.Api.DbUtility;
using ReceptyOks.Api.Middleware;
using ReceptyOks.Shared;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
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

    private const string TestJwtKey = "test-jwt-key-must-be-at-least-32-chars-long!";

    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    // Kept open for the factory's lifetime so the SQLite in-memory HomeSeeker DB survives
    // between requests (an in-memory SQLite database is dropped when its last connection closes).
    private readonly SqliteConnection _homeSeekerConnection = new("Data Source=:memory:");

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
                ["Jwt:Key"] = TestJwtKey
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

            // HomeSeeker: replace the file-backed SQLite DB with an isolated in-memory one.
            // The relational provider is kept because the repository relies on ExecuteUpdate,
            // which the EF InMemory provider does not support.
            services.RemoveAll<DbContextOptions<HomeSeekerDbContext>>();
            services.RemoveAll<HomeSeekerDbContext>();
            services.RemoveAll(typeof(IDbContextPool<HomeSeekerDbContext>));
            services.RemoveAll(typeof(IScopedDbContextLease<HomeSeekerDbContext>));

            _homeSeekerConnection.Open();
            services.AddDbContext<HomeSeekerDbContext>(options =>
            {
                options.UseSqlite(_homeSeekerConnection);
            });

            // With minimal hosting the factory's in-memory configuration is added *before*
            // the app's own sources, so a Jwt:Key from user secrets / environment variables
            // would win and tokens minted by CreateJwt would fail validation. Pin the
            // validation key explicitly so tests are independent of the developer machine.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
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

    /// <summary>
    /// Creates an HttpClient with the test API key header and a JWT bearer token
    /// whose <c>sub</c> claim is <paramref name="ownerId"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string ownerId)
    {
        var client = CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateJwt(ownerId));
        return client;
    }

    /// <summary>
    /// Mints a JWT signed with the test key for the given subject.
    /// </summary>
    public static string CreateJwt(string ownerId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, ownerId)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _homeSeekerConnection.Dispose();
        }
    }
}
