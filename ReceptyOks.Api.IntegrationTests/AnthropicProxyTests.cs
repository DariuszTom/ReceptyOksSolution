using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceptyOks.Shared;
using System.Net;
using System.Text;

namespace ReceptyOks.Api.IntegrationTests;

[TestFixture]
public class AnthropicProxyTests
{
    private const string UpstreamToken = "sk-ant-test-upstream-key";

    private WebApplication _upstream = null!;
    private CustomWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _proxyFactory = null!;
    private CapturedRequest? _captured;

    private sealed record CapturedRequest(string Path, string? ApiKey, string? Authorization, string Body);

    [SetUp]
    public async Task Setup()
    {
        _captured = null;

        var upstreamBuilder = WebApplication.CreateSlimBuilder();
        upstreamBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        _upstream = upstreamBuilder.Build();
        _upstream.MapPost("/v1/messages", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            _captured = new CapturedRequest(
                ctx.Request.Path,
                ctx.Request.Headers["x-api-key"].ToString(),
                ctx.Request.Headers.Authorization.ToString(),
                await reader.ReadToEndAsync());
            return Results.Json(new { id = "msg_test", type = "message" });
        });
        await _upstream.StartAsync();

        var upstreamUrl = _upstream.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        _factory = new CustomWebApplicationFactory();
        _proxyFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AnthropicProxy:Destination", upstreamUrl + "/");
            // Added last so it overrides user secrets / environment variables.
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AnthropicProxy:Destination"] = upstreamUrl + "/",
                    ["Token"] = UpstreamToken
                }));
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        _proxyFactory.Dispose();
        _factory.Dispose();
        await _upstream.DisposeAsync();
    }

    [Test]
    public async Task Messages_WithValidApiKey_ForwardsWithServerSideAnthropicKey()
    {
        using var client = _proxyFactory.CreateClient();
        client.DefaultRequestHeaders.Add(GlobalConstants.ApiKeyHeaderName, CustomWebApplicationFactory.TestApiKey);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "client-jwt");

        const string body = """{"model":"m","max_tokens":10,"messages":[{"role":"user","content":"hi"}]}""";
        var response = await client.PostAsync("/api/ai/v1/messages", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_captured!.Path, Is.EqualTo("/v1/messages"));
            Assert.That(_captured.ApiKey, Is.EqualTo(UpstreamToken));
            Assert.That(_captured.ApiKey, Does.Not.Contain(CustomWebApplicationFactory.TestApiKey));
            Assert.That(_captured.Authorization, Is.Empty);
            Assert.That(_captured.Body, Is.EqualTo(body));
        });
    }

    [Test]
    public async Task Messages_WithoutApiKey_ReturnsUnauthorizedAndDoesNotForward()
    {
        using var client = _proxyFactory.CreateClient();

        var response = await client.PostAsync("/api/ai/v1/messages", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_captured, Is.Null);
    }

    [Test]
    public async Task OtherUpstreamPaths_AreNotProxied()
    {
        using var client = _proxyFactory.CreateClient();
        client.DefaultRequestHeaders.Add(GlobalConstants.ApiKeyHeaderName, CustomWebApplicationFactory.TestApiKey);

        var response = await client.GetAsync("/api/ai/v1/models");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(_captured, Is.Null);
    }
}
