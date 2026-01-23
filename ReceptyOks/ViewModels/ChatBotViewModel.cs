using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared.AI;
using System.Collections.ObjectModel;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for the chatbot page, managing conversation with the AI agent.
/// </summary>
public partial class ChatBotViewModel : ObservableObject
{
    private readonly TokenProviderService _tokenProvider;
    private readonly ILogger _logger;
 private readonly AgentToolsRegistrar _toolsRegistrar;
    private AiAgent? _agent;
    private CancellationTokenSource? _sendCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _userInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private bool _isInitializing;

    [ObservableProperty]
    private string _initializationError = string.Empty;

    [ObservableProperty]
    private bool _hasInitializationError;

    /// <summary>
    /// Gets the collection of chat messages displayed in the UI.
    /// </summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public ChatBotViewModel(LocalDatabase database, TokenProviderService tokenProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
     ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(logger);

      _logger = logger;
        _toolsRegistrar = new AgentToolsRegistrar(database, logger);
        _tokenProvider = tokenProvider;
 }

    /// <summary>
    /// Initializes the AI agent with Anthropic client and registers tools.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_agent is not null)
        {
            return; // Already initialized
        }

        IsInitializing = true;
        HasInitializationError = false;
        InitializationError = string.Empty;

        try
        {
            _logger.Information("Initializing AI agent...");

            // Get API token from backend
            var tokenResponse = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.Token))
            {
                throw new InvalidOperationException("Failed to retrieve API token from backend");
            }

            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(tokenResponse.Token);

            var settings = new AnthropicSettings();

 using (var anthritopicAgent = new AnthropicAgent(settings, tokenBytes))
        {
   _agent = new AiAgent(anthritopicAgent.GetAgent(), settings.SystemPrompt);
 }

   // Register tools
 _toolsRegistrar.RegisterTools(_agent);

      _logger.Information("AI agent initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize AI agent");
            await MainThread.InvokeOnMainThreadAsync(() =>
              {
                  HasInitializationError = true;
                  InitializationError = "Nie udało się zainicjalizować asystenta AI. Spróbuj ponownie.";
              });
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private bool CanSendMessage => !string.IsNullOrWhiteSpace(UserInput) && !IsBusy && !IsInitializing && _agent is not null;

    /// <summary>
    /// Sends the user's message to the AI agent and streams the response.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || _agent is null)
        {
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;

        var userMessage = UserInput.Trim();
        UserInput = string.Empty;

        // Add user message to UI
        Messages.Add(new ChatMessageViewModel(userMessage, isUser: true));

        // Add placeholder for assistant response
        var assistantMessage = new ChatMessageViewModel(string.Empty, isUser: false);
        Messages.Add(assistantMessage);

        IsBusy = true;
        _sendCts = new CancellationTokenSource();

        try
        {
            await _agent.ChatStreamAsync(
                userMessage,
                chunk => MainThread.BeginInvokeOnMainThread(() => assistantMessage.Content += chunk),
                _sendCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Chat message sending was cancelled");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (string.IsNullOrEmpty(assistantMessage.Content))
                {
                    assistantMessage.Content = "[Anulowano]";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send chat message");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HasError = true;
                ErrorMessage = "Nie udało się wysłać wiadomości. Spróbuj ponownie.";
                assistantMessage.Content = "[Błąd - spróbuj ponownie]";
            });
        }
        finally
        {
            // Ensure UI-affecting state changes happen on the main thread.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = false;
                _sendCts?.Dispose();
                _sendCts = null;
            });
        }
    }

    private bool CanCancel => IsBusy;

    /// <summary>
    /// Cancels the current message sending operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _sendCts?.Cancel();
    }

    /// <summary>
    /// Clears all messages and resets the conversation.
    /// </summary>
    [RelayCommand]
    private void ClearConversation()
    {
        Messages.Clear();
        _agent?.ClearHistory();
        HasError = false;
        ErrorMessage = string.Empty;
        _logger.Information("Conversation cleared");
    }

    /// <summary>
    /// Retries initialization after an error.
    /// </summary>
    [RelayCommand]
    private async Task RetryInitializationAsync()
    {
        await InitializeAsync();
    }
}

/// <summary>
/// Represents a single chat message for display in the UI.
/// </summary>
public partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _content;

    /// <summary>
    /// Gets a value indicating whether this message is from the user.
    /// </summary>
    public bool IsUser { get; }

    /// <summary>
    /// Gets a value indicating whether this message is from the assistant.
    /// </summary>
    public bool IsAssistant => !IsUser;

    public ChatMessageViewModel(string content, bool isUser)
    {
        _content = content;
        IsUser = isUser;
    }
}
