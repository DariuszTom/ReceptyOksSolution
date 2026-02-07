using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI
{
    public interface IAiAgent
    {
        string? SystemPrompt { get; set; }
        IReadOnlyList<AITool> Tools { get; }
        string? ConversationId { get; }
 Task<string> ChatAsync(string userMessage, int maxToolRounds = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message and parses the response as a structured object.
     /// </summary>
        /// <typeparam name="T">The type to deserialize the response to.</typeparam>
     Task<T?> ChatAsync<T>(string userMessage, int maxToolRounds = 5, CancellationToken cancellationToken = default) where T : class;

  Task<string> ChatStreamAsync(string userMessage, Action<string> onTextReceived, CancellationToken cancellationToken = default);
    void ClearHistory();
      void ClearTools();
  Task<string> SaveConversationAsync(CancellationToken cancellationToken = default);
        Task LoadConversationAsync(string serializedThread, string? conversationId = null, CancellationToken cancellationToken = default);
    }
}