using ReceptyOks.Interfaces;
using ReceptyOks.Shared.AI;

namespace ReceptyOks.Services.AgentTools;

/// <summary>
/// Composite over all <see cref="IAgentToolProvider"/> instances registered in DI.
/// </summary>
public sealed class AgentToolsRegistrar(IEnumerable<IAgentToolProvider> providers) : IAgentToolsRegistrar
{
    private readonly IReadOnlyList<IAgentToolProvider> _providers =
        providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));

    public void RegisterTools(AiAgent agent, params AgentToolGroup[] groups)
    {
        ArgumentNullException.ThrowIfNull(agent);

        foreach (var provider in _providers)
        {
            if (groups.Length == 0 || groups.Contains(provider.Group))
            {
                provider.Register(agent);
            }
        }
    }
}
