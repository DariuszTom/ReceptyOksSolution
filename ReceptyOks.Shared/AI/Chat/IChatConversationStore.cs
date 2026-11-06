namespace ReceptyOks.Shared.AI.Chat;

/// <summary>
/// A persisted chat conversation (serialized agent thread plus display metadata).
/// </summary>
public sealed record ChatConversation(string Id, string? Title, DateTimeOffset UpdatedAt, string SerializedThread);

/// <summary>
/// Persistence abstraction for chat conversation history, independent of the storage technology.
/// </summary>
public interface IChatConversationStore
{
    /// <summary>Returns all conversations ordered by most recent first.</summary>
    Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a conversation by id, or <c>null</c> when it does not exist.</summary>
    Task<ChatConversation?> GetConversationAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates a conversation.</summary>
    Task SaveConversationAsync(string id, string title, string serializedThread, CancellationToken cancellationToken = default);

    /// <summary>Deletes a conversation.</summary>
    Task DeleteConversationAsync(string id, CancellationToken cancellationToken = default);
}
