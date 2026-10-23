using Microsoft.Extensions.AI;
using ReceptyOks.Configuration;
using ReceptyOks.Shared.AI;

namespace ReceptyOks.Services;

/// <summary>
/// Creates Anthropic chat clients that go through the backend reverse proxy (<c>/api/ai</c>).
/// The Anthropic API key never reaches the device - requests are authenticated with the
/// application API key (added by <see cref="ApiKeyHandler"/>) and the backend injects the real key.
/// </summary>
public sealed class AiChatClientFactory
{
    public const string HttpClientName = "AnthropicProxy";
    private const string ProxyPath = "/api/ai";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _appSettings;

    public AiChatClientFactory(IHttpClientFactory httpClientFactory, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(appSettings);
        _httpClientFactory = httpClientFactory;
        _appSettings = appSettings;
    }

    public IChatClient CreateChatClient(AnthropicSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.BaseUrl = _appSettings.Http.GetEffectiveApiUrl().TrimEnd('/') + ProxyPath;

        using var agent = new AnthropicAgent(settings, _httpClientFactory.CreateClient(HttpClientName));
        return agent.GetAgent();
    }
}
