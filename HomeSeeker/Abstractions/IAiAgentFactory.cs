using ReceptyOks.Shared.AI;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Factory for creating AI agent instances.
/// Creates a fresh agent per evaluation to avoid session state issues.
/// </summary>
public interface IAiAgentFactory
{
    /// <summary>
    /// Creates a new AI agent instance.
    /// </summary>
    /// <param name="systemPrompt">System prompt for the agent.</param>
    /// <param name="withWebBrowsing">Whether to register web browsing tools.</param>
    /// <returns>A new AI agent instance.</returns>
    IAiAgent CreateAgent(string? systemPrompt = null, bool withWebBrowsing = false);
}
