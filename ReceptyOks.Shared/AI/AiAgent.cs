using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ReceptyOks.Shared.AI;

/// <summary>
/// Implementation of IAiAgent using Microsoft Agent Framework (Microsoft.Agents.AI).
/// Wraps ChatClientAgent to provide chat interactions with function calling support.
/// Supports conversation persistence through AgentThread serialization.
/// Note: This implementation does not support fluent chaining for AddTool methods.
/// Use separate statements for adding tools.
/// </summary>
public sealed class AiAgent : IAiAgent
{
    private readonly IChatClient _chatClient;
    private readonly List<AITool> _tools = [];
    private string? _systemPrompt;
    private AgentThread? _thread;
    private string? _conversationId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiAgent"/> class.
    /// </summary>
    /// <param name="chatClient">The chat client implementation (Anthropic, OpenAI, Azure, etc.).</param>
    /// <param name="systemPrompt">Optional system prompt to guide the AI behavior.</param>
    public AiAgent(IChatClient chatClient, string? systemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Gets or sets the system prompt.
    /// </summary>
    public string? SystemPrompt
    {
        get => _systemPrompt;
        set => _systemPrompt = value;
    }

    /// <summary>
    /// Gets the registered tools.
    /// </summary>
    public IReadOnlyList<AITool> Tools => _tools;

    /// <summary>
    /// Gets the current conversation ID, if any.
    /// </summary>
    public string? ConversationId => _conversationId;

    /// <summary>
    /// Registers a tool that the agent can use.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public void AddTool(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
    }

    /// <summary>
    /// Registers a function as a tool using AIFunctionFactory.
    /// </summary>
    /// <typeparam name="TResult">The return type of the function.</typeparam>
    /// <param name="func">The function to register.</param>
    /// <param name="name">Optional name for the function.</param>
    /// <param name="description">Optional description for the function.</param>
    public void AddTool<TResult>(Func<TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
    }

    /// <summary>
    /// Registers a function with one parameter as a tool.
    /// </summary>
    public void AddTool<T1, TResult>(Func<T1, TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
    }

    /// <summary>
    /// Registers a function with two parameters as a tool.
    /// </summary>
    public void AddTool<T1, T2, TResult>(Func<T1, T2, TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
    }

    /// <summary>
    /// Registers an async function with one parameter as a tool.
    /// </summary>
    public void AddToolAsync<T1, TResult>(Func<T1, Task<TResult>> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
    }

    /// <summary>
    /// Clears all registered tools.
    /// </summary>
    public void ClearTools() => _tools.Clear();

    /// <summary>
    /// Sends a message and receives a complete response.
    /// If tools are registered, automatically handles tool calls.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="maxToolRounds">Maximum number of tool call rounds (not directly supported by ChatClientAgent, but respected in logic).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI's final response text.</returns>
    public async Task<string> ChatAsync(
        string userMessage,
        int maxToolRounds = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        var agent = CreateAgent();
        _thread ??= await agent.GetNewThreadAsync(cancellationToken).ConfigureAwait(false);

        var response = await agent.RunAsync(userMessage, _thread, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return ExtractTextFromResponse(response);
    }

    /// <summary>
    /// Sends a message and streams the response with a callback for each text chunk.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="onTextReceived">Callback invoked for each text chunk received.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete response text.</returns>
    public async Task<string> ChatStreamAsync(string userMessage,
        Action<string> onTextReceived, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(onTextReceived);

        var agent = CreateAgent();
        _thread ??= await agent.GetNewThreadAsync(cancellationToken).ConfigureAwait(false);

        var fullResponse = new StringBuilder();

        await foreach (var update in agent.RunStreamingAsync(userMessage, _thread, cancellationToken: cancellationToken)
               .ConfigureAwait(false))
        {
            if (update.AsChatResponseUpdate() is { } chatUpdate)
            {
                foreach (var content in chatUpdate.Contents)
                {
                    if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        fullResponse.Append(textContent.Text);
                        onTextReceived(textContent.Text);
                    }
                }
            }
        }

        return fullResponse.ToString();
    }

    /// <summary>
    /// Clears the conversation history to start a new conversation.
    /// </summary>
    public void ClearHistory()
    {
        _thread = null;
        _conversationId = null;
    }

    /// <summary>
    /// Serializes the current conversation thread to JSON format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON string representing the serialized conversation, or null if no active conversation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no active conversation to save.</exception>
    public Task<string> SaveConversationAsync(CancellationToken cancellationToken = default)
    {
        if (_thread is null)
        {
            throw new InvalidOperationException("No active conversation to save. Start a conversation first by calling ChatAsync or ChatStreamAsync.");
        }

        // Serialize the thread to JsonElement
        var serializedThread = _thread.Serialize(JsonSerializerOptions.Web);

        // Generate conversation ID if not already set
        _conversationId ??= Guid.NewGuid().ToString();

        // Return as JSON string
        return Task.FromResult(serializedThread.GetRawText());
    }

    /// <summary>
    /// Loads a previously saved conversation from serialized JSON.
    /// </summary>
    /// <param name="serializedThread">The JSON string containing the serialized conversation.</param>
    /// <param name="conversationId">Optional conversation ID to associate with this thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown if serializedThread is null or whitespace.</exception>
    public async Task LoadConversationAsync(string serializedThread, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedThread);

        var agent = CreateAgent();

        // Deserialize the thread from JSON
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(serializedThread, JsonSerializerOptions.Web);
        _thread = await agent.DeserializeThreadAsync(jsonElement, JsonSerializerOptions.Web, cancellationToken)
             .ConfigureAwait(false);

        _conversationId = conversationId;
    }

    private ChatClientAgent CreateAgent()
    {
        // Pass instructions and tools directly to constructor
        return new ChatClientAgent(_chatClient, instructions: _systemPrompt,
            tools: _tools.Count > 0 ? _tools : null);
    }

    private static string ExtractTextFromResponse(AgentResponse response)
    {
        var textBuilder = new StringBuilder();

        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                {
                    textBuilder.Append(textContent.Text);
                }
            }
        }

        return textBuilder.ToString();
    }
}
