using ReceptyOks.Interfaces;
using ReceptyOks.Shared.AI;

namespace ReceptyOks.Services.AgentTools;

/// <summary>
/// Adapts <see cref="WebBrowsingTool"/> to <see cref="IAgentToolProvider"/>. Registered as a typed
/// <see cref="HttpClient"/> client, so the HttpClient is owned by IHttpClientFactory, not by this class.
/// </summary>
public sealed class WebBrowsingToolProvider(HttpClient httpClient) : IAgentToolProvider
{
    private readonly WebBrowsingTool _tool = new(httpClient ?? throw new ArgumentNullException(nameof(httpClient)));

    public AgentToolGroup Group => AgentToolGroup.Web;

    public void Register(AiAgent agent) => _tool.RegisterTools(agent);
}
