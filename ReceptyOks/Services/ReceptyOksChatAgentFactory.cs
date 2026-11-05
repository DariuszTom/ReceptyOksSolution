using ReceptyOks.Interfaces;
using ReceptyOks.Shared.AI;
using ReceptyOks.Shared.AI.Chat;

namespace ReceptyOks.Services;

/// <summary>
/// Builds the ReceptyOks assistant: Anthropic client via backend proxy, personalised system prompt
/// and the app's tool groups (recipes, HomeSeeker, web).
/// </summary>
public sealed class ReceptyOksChatAgentFactory(AiChatClientFactory chatClientFactory, IAgentToolsRegistrar toolsRegistrar) : IChatAgentFactory
{
    private readonly AiChatClientFactory _chatClientFactory = chatClientFactory ?? throw new ArgumentNullException(nameof(chatClientFactory));
    private readonly IAgentToolsRegistrar _toolsRegistrar = toolsRegistrar ?? throw new ArgumentNullException(nameof(toolsRegistrar));

    public async Task<IAiAgent> CreateAgentAsync(CancellationToken cancellationToken = default)
    {
        var settings = new AnthropicSettings();

        string prompt = settings.SystemPrompt;
        if (await UserService.Instance.Value.HasUserAsync())
        {
            var user = await UserService.Instance.Value.GetUserAsync();
            prompt = prompt.Replace("{UserName}", user.Name ?? "Nie podano");
        }

        var agent = new AiAgent(_chatClientFactory.CreateChatClient(settings), prompt);
        _toolsRegistrar.RegisterTools(agent, AgentToolGroup.Recipes, AgentToolGroup.HomeSeeker, AgentToolGroup.Web);
        return agent;
    }
}
