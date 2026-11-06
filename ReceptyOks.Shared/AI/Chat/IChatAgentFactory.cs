namespace ReceptyOks.Shared.AI.Chat;

/// <summary>
/// Creates a fully configured chat agent (chat client, system prompt and tools).
/// Keeps app-specific concerns such as user profile or tool sets out of the chat UI.
/// </summary>
public interface IChatAgentFactory
{
    Task<IAiAgent> CreateAgentAsync(CancellationToken cancellationToken = default);
}
