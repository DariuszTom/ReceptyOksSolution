namespace ReceptyOks.Shared.Models;

/// <summary>
/// Represents metadata about a saved conversation with the AI agent.
/// </summary>
public sealed class ConversationMetadata
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Optional title or preview of the conversation.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// When the conversation was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
public DateTimeOffset UpdatedAt { get; set; }

  /// <summary>
    /// Serialized AgentThread data (JSON).
    /// </summary>
    public string SerializedThread { get; set; } = string.Empty;
}
