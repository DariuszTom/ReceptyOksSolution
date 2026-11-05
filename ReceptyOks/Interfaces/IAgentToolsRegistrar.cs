using ReceptyOks.Shared.AI;

namespace ReceptyOks.Interfaces
{
    /// <summary>Registers AI agent tools contributed by <see cref="IAgentToolProvider"/> implementations.</summary>
    public interface IAgentToolsRegistrar
    {
        /// <summary>Registers tools of the given groups; when none are specified, every group is registered.</summary>
        void RegisterTools(AiAgent agent, params AgentToolGroup[] groups);
    }
}