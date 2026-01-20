using Microsoft.Extensions.AI;

namespace ReceptyOks.Shared.AI;

/// <summary>
/// Provider-agnostic AI agent for chat interactions using Microsoft.Extensions.AI abstractions.
/// Supports tools/function calling for agentic scenarios.
/// </summary>
public sealed class AiAgent
{
    private readonly IChatClient _chatClient;
    private readonly List<ChatMessage> _conversationHistory = [];
    private readonly List<AITool> _tools = [];
    private string? _systemPrompt;

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
    /// Gets the current conversation history count.
    /// </summary>
    public int MessageCount => _conversationHistory.Count;

    /// <summary>
    /// Registers a tool that the agent can use.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public AiAgent AddTool(AITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
        return this;
    }

    /// <summary>
    /// Registers a function as a tool using AIFunctionFactory.
    /// </summary>
    /// <typeparam name="TResult">The return type of the function.</typeparam>
    /// <param name="func">The function to register.</param>
    /// <param name="name">Optional name for the function.</param>
    /// <param name="description">Optional description for the function.</param>
    public AiAgent AddTool<TResult>(Func<TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
        return this;
    }

    /// <summary>
    /// Registers a function with one parameter as a tool.
    /// </summary>
    public AiAgent AddTool<T1, TResult>(Func<T1, TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
        return this;
    }

    /// <summary>
    /// Registers a function with two parameters as a tool.
    /// </summary>
    public AiAgent AddTool<T1, T2, TResult>(Func<T1, T2, TResult> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
        return this;
    }

    /// <summary>
    /// Registers an async function with one parameter as a tool.
    /// </summary>
    public AiAgent AddToolAsync<T1, TResult>(Func<T1, Task<TResult>> func, string? name = null, string? description = null)
    {
        var aiFunc = AIFunctionFactory.Create(func, name, description);
        _tools.Add(aiFunc);
        return this;
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
    /// <param name="maxToolRounds">Maximum number of tool call rounds (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI's final response text.</returns>
    public async Task<string> ChatAsync(
        string userMessage,
        int maxToolRounds = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        _conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var options = CreateChatOptions();
        var toolRound = 0;

        while (toolRound < maxToolRounds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messages = BuildMessages();
            var response = await _chatClient.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);

            // Check if there are tool calls to process
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();

            if (toolCalls.Count == 0)
            {
                // No tool calls, we have the final response
                var assistantMessage = response.Text ?? string.Empty;
                _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, assistantMessage));
                return assistantMessage;
            }

            // Add assistant message with tool calls
            _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, [.. response.Messages.SelectMany(m => m.Contents)]));

            // Process each tool call
            foreach (var toolCall in toolCalls)
            {
                var result = await ProcessToolCallAsync(toolCall, cancellationToken).ConfigureAwait(false);
                _conversationHistory.Add(new ChatMessage(ChatRole.Tool, [result]));
            }

            toolRound++;
        }

        // Max rounds reached, return last response
        var finalMessages = BuildMessages();
        var finalResponse = await _chatClient.GetResponseAsync(finalMessages, options, cancellationToken)
            .ConfigureAwait(false);

        var finalText = finalResponse.Text ?? string.Empty;
        _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, finalText));
        return finalText;
    }

    /// <summary>
    /// Sends a message and streams the response with a callback for each text chunk.
    /// Note: Tool calls are not supported in streaming mode.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="onTextReceived">Callback invoked for each text chunk received.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete response text.</returns>
    public async Task<string> ChatStreamAsync(
        string userMessage,
        Action<string> onTextReceived,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(onTextReceived);

        _conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        var messages = BuildMessages();
        var options = CreateChatOptions();
        var fullResponse = new StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (update.Text is { } text)
            {
                fullResponse.Append(text);
                onTextReceived(text);
            }
        }

        var result = fullResponse.ToString();
        _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, result));

        return result;
    }

    /// <summary>
    /// Clears the conversation history to start a new conversation.
    /// </summary>
    public void ClearHistory() => _conversationHistory.Clear();

    private ChatOptions? CreateChatOptions()
    {
        if (_tools.Count == 0)
        {
            return null;
        }

        return new ChatOptions
        {
            Tools = [.. _tools]
        };
    }

    private async Task<FunctionResultContent> ProcessToolCallAsync(
        FunctionCallContent toolCall,
        CancellationToken cancellationToken)
    {
        var tool = _tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == toolCall.Name);

        if (tool is null)
        {
            return new FunctionResultContent(toolCall.CallId, $"Tool '{toolCall.Name}' not found.");
        }

        try
        {
            var arguments = toolCall.Arguments is not null
                ? new AIFunctionArguments(toolCall.Arguments)
                : null;
            var result = await tool.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
            return new FunctionResultContent(toolCall.CallId, result);
        }
        catch (Exception ex)
        {
            return new FunctionResultContent(toolCall.CallId, $"Error: {ex.Message}");
        }
    }

    private List<ChatMessage> BuildMessages()
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, _systemPrompt));
        }

        messages.AddRange(_conversationHistory);
        return messages;
    }
}
