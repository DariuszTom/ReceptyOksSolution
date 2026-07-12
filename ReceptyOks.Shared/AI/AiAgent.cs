using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI;

/// <summary>
/// Implementation of IAiAgent using Microsoft Agent Framework (Microsoft.Agents.AI).
/// Wraps ChatClientAgent to provide chat interactions with function calling support.
/// Supports conversation persistence through AgentSession serialization.
/// Note: This implementation does not support fluent chaining for AddTool methods.
/// Use separate statements for adding tools.
/// </summary>
public sealed class AiAgent : IAiAgent
{
    private readonly IChatClient _chatClient;
    private readonly List<AITool> _tools = [];
    private string? _systemPrompt;
    private AgentSession? _session;
    private string? _conversationId;
    private ChatClientAgent? _agent;

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
    public void ClearTools()
    {
        _tools.Clear();
        InvalidateAgent();
    }

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

        var agent = GetOrCreateAgent();
        _session ??= await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);

        var response = await agent.RunAsync(userMessage, _session, cancellationToken: cancellationToken)
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

        var agent = GetOrCreateAgent();
        _session ??= await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);

        var fullResponse = new StringBuilder();

        await foreach (var update in agent.RunStreamingAsync(userMessage, _session, cancellationToken: cancellationToken)
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
        _session = null;
        _conversationId = null;
        // Don't invalidate agent - it can be reused with a new session
    }

    /// <summary>
    /// Extracts conversation messages from a serialized session JSON.
    /// Parses the JSON structure to retrieve user and assistant messages.
    /// </summary>
    /// <param name="serializedThread">The JSON string containing the serialized conversation.</param>
    /// <returns>List of conversation messages with role and content.</returns>
    public static IReadOnlyList<ConversationMessage> GetConversationHistory(string serializedThread)
        => ConversationHistoryParser.Parse(serializedThread);

    /// <summary>
    /// Serializes the current conversation session to JSON format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON string representing the serialized conversation, or null if no active conversation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no active conversation to save.</exception>
    public async Task<string> SaveConversationAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("No active conversation to save. Start a conversation first by calling ChatAsync or ChatStreamAsync.");
        }

        // Serialize the session to JsonElement
        var agent = GetOrCreateAgent();
        var serializedSession = await agent.SerializeSessionAsync(_session, JsonSerializerOptions.Web, cancellationToken)
            .ConfigureAwait(false);

        // Generate conversation ID if not already set
        _conversationId ??= Guid.NewGuid().ToString();

        // Return as JSON string
        return serializedSession.GetRawText();
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

        // Clear current session and invalidate agent to ensure clean state
        _session = null;
        InvalidateAgent();

        var agent = GetOrCreateAgent();

        // Deserialize the session from JSON
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(serializedThread, JsonSerializerOptions.Web);
        _session = await agent.DeserializeSessionAsync(jsonElement, JsonSerializerOptions.Web, cancellationToken)
             .ConfigureAwait(false);

        _conversationId = conversationId;
    }

    private ChatClientAgent GetOrCreateAgent()
    {
        // Reuse existing agent to maintain conversation state properly
        // Only create new agent if it doesn't exist or if tools have changed
        if (_agent is null)
        {
            _agent = new ChatClientAgent(_chatClient, instructions: _systemPrompt,
                tools: _tools.Count > 0 ? _tools : null);
        }
        return _agent;
    }

    /// <summary>
    /// Forces recreation of the agent (useful after tools are modified).
    /// </summary>
    private void InvalidateAgent()
    {
        _agent = null;
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

    /// <summary>
    /// Sends a message and parses the response as a structured object.
    /// The AI must return valid JSON matching the expected type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="maxToolRounds">Maximum number of tool call rounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response object, or default if parsing fails.</returns>
    public async Task<T?> ChatAsync<T>(
        string userMessage,
        int maxToolRounds = 5,
        CancellationToken cancellationToken = default) where T : class
    {
        var responseText = await ChatAsync(userMessage, maxToolRounds, cancellationToken).ConfigureAwait(false);
        return ParseJsonResponse<T>(responseText);
    }

    /// <summary>
    /// Parses a JSON response, extracting JSON from markdown code blocks if present.
    /// </summary>
    private static T? ParseJsonResponse<T>(string responseText) where T : class
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return default;

        var json = ExtractJsonFromResponse(responseText);

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// Extracts JSON content from a response that may contain markdown code blocks.
    /// </summary>
    private static string ExtractJsonFromResponse(string responseText)
    {
        var text = responseText.Trim();

        // Check for markdown JSON code block
        const string jsonBlockStart = "```json";
        const string blockEnd = "```";

        var jsonStart = text.IndexOf(jsonBlockStart, StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            var contentStart = jsonStart + jsonBlockStart.Length;
            var jsonEnd = text.IndexOf(blockEnd, contentStart, StringComparison.Ordinal);
            if (jsonEnd > contentStart)
            {
                return text[contentStart..jsonEnd].Trim();
            }
        }

        // Check for generic code block that might contain JSON
        const string genericBlockStart = "```";
        var genericStart = text.IndexOf(genericBlockStart, StringComparison.Ordinal);
        if (genericStart >= 0)
        {
            var contentStart = text.IndexOf('\n', genericStart);
            if (contentStart >= 0)
            {
                contentStart++;
                var genericEnd = text.IndexOf(blockEnd, contentStart, StringComparison.Ordinal);
                if (genericEnd > contentStart)
                {
                    return text[contentStart..genericEnd].Trim();
                }
            }
        }

        // If no code block, assume the entire text is JSON
        return text;
    }
}

/// <summary>
/// Represents a message in conversation history.
/// </summary>
/// <param name="Content">The message content.</param>
/// <param name="IsUser">True if the message is from the user, false if from the assistant.</param>
public sealed record ConversationMessage(string Content, bool IsUser);
