using System.Text;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using Microsoft.Extensions.Options;
using ReceptyOks.Shared.AI;

namespace ReceptyOks.Api.Services;

/// <summary>
/// Factory for creating AI agent instances using Anthropic API.
/// Creates a fresh agent per evaluation to avoid session state issues.
/// </summary>
public sealed class AnthropicAiAgentFactory : IAiAgentFactory
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HomeSeekerOptions _options;

    public AnthropicAiAgentFactory(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public IAiAgent CreateAgent(string? systemPrompt = null, bool withWebBrowsing = false)
    {
        // Get API token from configuration (available via SecretsResolver / Key Vault)
        var token = _configuration["Token"]
            ?? throw new InvalidOperationException("Anthropic API token not configured. Ensure 'Token' is set in configuration.");

        var tokenBytes = Encoding.UTF8.GetBytes(token);

        var settings = new AnthropicSettings
        {
            Model = _options.Model,
            MaxTokens = 4096,
            BaseUrl = "https://api.anthropic.com"
        };

        var anthropicAgent = new AnthropicAgent(settings, tokenBytes);
        var chatClient = anthropicAgent.GetAgent();

        var agent = new AiAgent(chatClient, systemPrompt);

        if (withWebBrowsing)
        {
            var httpClient = _httpClientFactory.CreateClient("homeseeker-scraper");
            var webBrowsingTool = new WebBrowsingTool(httpClient, maxContentLength: 50000);
            webBrowsingTool.RegisterTools(agent);
        }

        return agent;
    }
}
