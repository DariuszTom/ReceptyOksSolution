using ReceptyOks.Shared.AI;

namespace ReceptyOks.Interfaces;

/// <summary>Logical group of AI agent tools; agents opt into the groups they need.</summary>
public enum AgentToolGroup
{
    Recipes,
    ShoppingList,
    HomeSeeker,
    Web
}

/// <summary>
/// A cohesive set of AI agent tools. Implementations are registered in DI and composed by
/// <see cref="IAgentToolsRegistrar"/>; adding a new tool set requires no changes to existing code.
/// </summary>
public interface IAgentToolProvider
{
    AgentToolGroup Group { get; }

    void Register(AiAgent agent);
}
