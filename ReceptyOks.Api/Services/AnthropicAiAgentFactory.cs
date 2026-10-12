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
/// <remarks>
/// Web browsing has been intentionally disabled for HomeSeeker agents (see
/// CODE_REVIEW.md C4). The previous WebBrowsingTool fetched arbitrary URLs
/// from a scoped shared HttpClient and was susceptible to SSRF and prompt
/// injection via scraped listing content. Evaluations now operate on
/// pre-scraped metadata only; if a future scenario needs browsing, it must
/// introduce an allow-listed, DNS-validated fetcher - not the shared tool.
/// </remarks>
public sealed class AnthropicAiAgentFactory : IAiAgentFactory
{
    private readonly IConfiguration _configuration;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<AnthropicAiAgentFactory> _logger;

    public AnthropicAiAgentFactory(
        IConfiguration configuration,
        IOptions<HomeSeekerOptions> options,
        ILogger<AnthropicAiAgentFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    public IAiAgent CreateAgent(string? systemPrompt = null, bool withWebBrowsing = false)
    {
        if (withWebBrowsing)
        {
            // Intentionally refuse rather than silently honour the request:
            // callers must consciously opt into any replacement that uses an
            // allow-listed fetcher.
            _logger.LogWarning(
                "CreateAgent called with withWebBrowsing=true; ignoring - web browsing is disabled for HomeSeeker agents (C4).");
        }

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

        return new AiAgent(chatClient, systemPrompt);
    }
}
