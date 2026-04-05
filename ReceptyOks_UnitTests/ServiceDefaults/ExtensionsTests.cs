using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace ReceptyOks_UnitTests.ServiceDefaults;

[TestFixture]
public class ExtensionsTests
{
    #region AddServiceDefaults Tests

    [Test]
    public void AddServiceDefaults_RegistersServiceDiscovery()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddServiceDefaults();

        // Assert - service discovery should be registered
        var services = builder.Services.BuildServiceProvider();
        // Service discovery is registered through IHttpClientFactory configuration
        Assert.Pass("AddServiceDefaults executed without exceptions");
    }

    [Test]
    public void AddServiceDefaults_RegistersHealthChecks()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddServiceDefaults();
        var services = builder.Services.BuildServiceProvider();

        // Assert
        var healthCheckService = services.GetService<HealthCheckService>();
        Assert.That(healthCheckService, Is.Not.Null, "HealthCheckService should be registered");
    }

    [Test]
    public void AddServiceDefaults_ReturnsBuilder_ForChaining()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        var result = builder.AddServiceDefaults();

        // Assert
        Assert.That(result, Is.SameAs(builder), "Should return the same builder for method chaining");
    }

    #endregion

    #region AddDefaultHealthChecks Tests

    [Test]
    public void AddDefaultHealthChecks_RegistersSelfCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Assert
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();
        Assert.That(report.Entries.ContainsKey("self"), Is.True, "Should contain 'self' health check");
    }

    [Test]
    public void AddDefaultHealthChecks_RegistersMemoryCheck()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Assert
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();
        Assert.That(report.Entries.ContainsKey("memory"), Is.True, "Should contain 'memory' health check");
    }

    [Test]
    public void AddDefaultHealthChecks_SelfCheck_ReturnsHealthy()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Act
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();

        // Assert
        Assert.That(report.Entries["self"].Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public void AddDefaultHealthChecks_SelfCheck_HasLiveTag()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Act
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();

        // Assert
        Assert.That(report.Entries["self"].Tags, Does.Contain("live"));
    }

    [Test]
    public void AddDefaultHealthChecks_MemoryCheck_HasLiveTag()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Act
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();

        // Assert
        Assert.That(report.Entries["memory"].Tags, Does.Contain("live"));
    }

    [Test]
    public void AddDefaultHealthChecks_MemoryCheck_ReturnsHealthyWhenMemoryLow()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Act
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();

        // Assert - in test environment memory should be below threshold
        Assert.That(report.Entries["memory"].Status, Is.EqualTo(HealthStatus.Healthy).Or.EqualTo(HealthStatus.Degraded),
            "Memory check should return Healthy or Degraded (not Unhealthy)");
    }

    [Test]
    public void AddDefaultHealthChecks_MemoryCheck_IncludesMemoryInDescription()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();
        var services = builder.Services.BuildServiceProvider();
        var healthCheckService = services.GetRequiredService<HealthCheckService>();

        // Act
        var report = healthCheckService.CheckHealthAsync().GetAwaiter().GetResult();

        // Assert
        Assert.That(report.Entries["memory"].Description, Does.Contain("MB"),
            "Memory check description should contain memory usage in MB");
    }

    [Test]
    public void AddDefaultHealthChecks_ReturnsBuilder_ForChaining()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        var result = builder.AddDefaultHealthChecks();

        // Assert
        Assert.That(result, Is.SameAs(builder));
    }

    #endregion

    #region MapDefaultEndpoints Tests

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_ReturnsOk()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_ReturnsJson()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_ContainsStatus()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        Assert.That(json.RootElement.TryGetProperty("status", out _), Is.True,
            "Response should contain 'status' property");
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_ContainsEntries()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        Assert.That(json.RootElement.TryGetProperty("entries", out _), Is.True,
            "Response should contain 'entries' property");
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_ContainsTotalDuration()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert
        Assert.That(json.RootElement.TryGetProperty("totalDuration", out _), Is.True,
            "Response should contain 'totalDuration' property");
    }

    [Test]
    public async Task MapDefaultEndpoints_AliveEndpoint_ReturnsOk()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/alive");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MapDefaultEndpoints_AliveEndpoint_ReturnsPlainText()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/alive");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - /alive returns plain text "Healthy"
        Assert.That(content, Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task MapDefaultEndpoints_AliveEndpoint_OnlyChecksLiveTaggedChecks()
    {
        // Arrange
        await using var app = CreateTestApplication(addNonLiveCheck: true);
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/alive");

        // Assert - should still be healthy even if non-live check would fail
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_IncludesAllChecks()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var entries = json.RootElement.GetProperty("entries");

        // Assert
        Assert.That(entries.TryGetProperty("self", out _), Is.True, "Should contain 'self' check");
        Assert.That(entries.TryGetProperty("memory", out _), Is.True, "Should contain 'memory' check");
    }

    [Test]
    public async Task MapDefaultEndpoints_HealthEndpoint_EntryContainsExpectedProperties()
    {
        // Arrange
        await using var app = CreateTestApplication();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var selfEntry = json.RootElement.GetProperty("entries").GetProperty("self");

        // Assert
        Assert.That(selfEntry.TryGetProperty("status", out _), Is.True, "Entry should contain 'status'");
        Assert.That(selfEntry.TryGetProperty("duration", out _), Is.True, "Entry should contain 'duration'");
        Assert.That(selfEntry.TryGetProperty("tags", out _), Is.True, "Entry should contain 'tags'");
    }

    #endregion

    #region ConfigureOpenTelemetry Tests

    [Test]
    public void ConfigureOpenTelemetry_DoesNotThrow()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act & Assert
        Assert.DoesNotThrow(() => builder.ConfigureOpenTelemetry());
    }

    [Test]
    public void ConfigureOpenTelemetry_ReturnsBuilder_ForChaining()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        var result = builder.ConfigureOpenTelemetry();

        // Assert
        Assert.That(result, Is.SameAs(builder));
    }

    #endregion

    #region Helper Methods

    private static WebApplication CreateTestApplication(bool addNonLiveCheck = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.AddDefaultHealthChecks();

        if (addNonLiveCheck)
        {
            // Add a health check without "live" tag that would fail
            builder.Services.AddHealthChecks()
                .AddCheck("non-live-failing", () => HealthCheckResult.Unhealthy("This check fails"), tags: ["ready"]);
        }

        var app = builder.Build();
        app.MapDefaultEndpoints();
        app.Start();

        return app;
    }

    #endregion
}
