using ReceptyOks.Shared.AI.Chat;

namespace ReceptyOks.Services;

/// <summary>
/// <see cref="IChatConversationStore"/> backed by the app's SQLite <see cref="ILocalDatabase"/>.
/// </summary>
public sealed class LocalChatConversationStore(ILocalDatabase database) : IChatConversationStore
{
    private readonly ILocalDatabase _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        var conversations = await _database.GetConversationsAsync().ConfigureAwait(false);
        return conversations.Select(ToModel).ToList();
    }

    public async Task<ChatConversation?> GetConversationAsync(string id, CancellationToken cancellationToken = default)
    {
        var conversation = await _database.GetConversationAsync(id).ConfigureAwait(false);
        return conversation is null ? null : ToModel(conversation);
    }

    public Task SaveConversationAsync(string id, string title, string serializedThread, CancellationToken cancellationToken = default)
        => _database.SaveConversationAsync(new ConversationLocal
        {
            Id = id,
            Title = title,
            SerializedThread = serializedThread,
        });

    public Task DeleteConversationAsync(string id, CancellationToken cancellationToken = default)
        => _database.DeleteConversationAsync(id);

    private static ChatConversation ToModel(ConversationLocal c) => new(c.Id, c.Title, c.UpdatedAt, c.SerializedThread);
}
