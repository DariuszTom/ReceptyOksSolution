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
        /// Sends a multimodal message (text + attachments such as images or PDFs) and receives a complete response.
        /// </summary>
        /// <param name="userMessage">A <see cref="ChatMessage"/> whose <see cref="ChatMessage.Contents"/> may combine
        /// <see cref="TextContent"/> and <see cref="DataContent"/> items.</param>
        Task<string> ChatAsync(ChatMessage userMessage, int maxToolRounds = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message and parses the response as a structured object.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to.</typeparam>
        Task<T?> ChatAsync<T>(string userMessage, int maxToolRounds = 5, CancellationToken cancellationToken = default) where T : class;

        Task<string> ChatStreamAsync(string userMessage, Action<string> onTextReceived, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a multimodal message and streams the textual response.
        /// </summary>
        Task<string> ChatStreamAsync(ChatMessage userMessage, Action<string> onTextReceived, CancellationToken cancellationToken = default);

        void ClearHistory();
        void ClearTools();
        Task<string> SaveConversationAsync(CancellationToken cancellationToken = default);
        Task LoadConversationAsync(string serializedThread, string? conversationId = null, CancellationToken cancellationToken = default);
    }
}