using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using ReceptyOks.Shared.AI;
using System.Collections.ObjectModel;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for the chatbot page, managing conversation with the AI agent.
/// </summary>
public partial class ChatBotViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly TokenProviderService _tokenProvider;
    private readonly ILogger _logger;
    private readonly AgentToolsRegistrar _toolsRegistrar;
    private AiAgent? _agent;
    private CancellationTokenSource? _sendCts;
    private string? _currentConversationId;

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

    [ObservableProperty]
    private bool _isHistorySheetOpen;

    [ObservableProperty]
    private bool _isLoadingHistory;

    /// <summary>
    /// Gets the collection of chat messages displayed in the UI.
    /// </summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    /// <summary>
    /// Gets the collection of saved conversations for the history list.
    /// </summary>
    public ObservableCollection<ConversationHistoryItemViewModel> Conversations { get; } = [];

    public ChatBotViewModel(LocalDatabase database, TokenProviderService tokenProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _database = database;
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
        _sendCts = new CancellationTokenSource(GlobalConstants.DefaultCancelationTokenTime*3);

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
        _currentConversationId = null;
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

    /// <summary>
    /// Opens the conversation history sheet.
    /// </summary>
    [RelayCommand]
    private async Task OpenHistoryAsync()
    {
        IsHistorySheetOpen = true;
        await LoadConversationsAsync();
    }

    /// <summary>
    /// Closes the conversation history sheet.
    /// </summary>
    [RelayCommand]
    private void CloseHistory()
    {
        IsHistorySheetOpen = false;
    }

    /// <summary>
    /// Loads all saved conversations from the database.
    /// </summary>
    private async Task LoadConversationsAsync()
    {
        if (IsLoadingHistory)
        {
            return;
        }

        IsLoadingHistory = true;

        try
        {
            var conversations = await _database.GetConversationsAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
  {
      Conversations.Clear();
      foreach (var conv in conversations)
      {
          Conversations.Add(new ConversationHistoryItemViewModel(
        conv.Id,
             conv.Title ?? "Rozmowa bez tytułu",
                   conv.UpdatedAt));
      }
  });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load conversations");
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    /// <summary>
    /// Loads a specific conversation from history.
    /// </summary>
    [RelayCommand]
    private async Task LoadConversationAsync(ConversationHistoryItemViewModel item)
    {
        if (item is null || _agent is null)
        {
            return;
        }

        IsHistorySheetOpen = false;
        IsBusy = true;

        try
        {
            var conversation = await _database.GetConversationAsync(item.Id).ConfigureAwait(false);
            if (conversation is null)
            {
                _logger.Warning("Conversation {Id} not found", item.Id);
                return;
            }

            // Load the conversation thread into the agent
            await _agent.LoadConversationAsync(conversation.SerializedThread, conversation.Id).ConfigureAwait(false);
            _currentConversationId = conversation.Id;

            await MainThread.InvokeOnMainThreadAsync(() =>
                     {
                         Messages.Clear();
                         // Note: Messages are not persisted separately; after loading, the user can continue the conversation
                         // A welcome message indicates the conversation was loaded
                         Messages.Add(new ChatMessageViewModel($"[Załadowano rozmowę: {item.Title}]", isUser: false));
                     });

            _logger.Information("Loaded conversation {Id}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load conversation {Id}", item.Id);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HasError = true;
                ErrorMessage = "Nie udało się załadować rozmowy.";
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    /// <summary>
    /// Saves the current conversation to the database.
    /// </summary>
    [RelayCommand]
    private async Task SaveConversationAsync()
    {
        if (_agent is null || Messages.Count == 0)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var serializedThread = await _agent.SaveConversationAsync().ConfigureAwait(false);
            _currentConversationId ??= Guid.NewGuid().ToString();

            // Generate a title from the first user message
            var firstUserMessage = Messages.FirstOrDefault(m => m.IsUser);
            var title = firstUserMessage?.Content?.Length > 50
           ? firstUserMessage.Content[..50] + "..."
            : firstUserMessage?.Content ?? "Rozmowa";

            var conversation = new ConversationLocal
            {
                Id = _currentConversationId,
                Title = title,
                SerializedThread = serializedThread
            };

            await _database.SaveConversationAsync(conversation).ConfigureAwait(false);

            _logger.Information("Conversation saved with ID {Id}", _currentConversationId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save conversation");
            await MainThread.InvokeOnMainThreadAsync(() =>
             {
                 HasError = true;
                 ErrorMessage = "Nie udało się zapisać rozmowy.";
             });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    /// <summary>
    /// Deletes a conversation from history.
    /// </summary>
    [RelayCommand]
    private async Task DeleteConversationAsync(ConversationHistoryItemViewModel item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _database.DeleteConversationAsync(item.Id).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
      {
          Conversations.Remove(item);
      });

            // If the deleted conversation is the current one, clear it
            if (_currentConversationId == item.Id)
            {
                ClearConversation();
            }

            _logger.Information("Conversation {Id} deleted", item.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete conversation {Id}", item.Id);
        }
    }

    /// <summary>
    /// Starts a new conversation, saving the current one if it has messages.
    /// </summary>
    [RelayCommand]
    private async Task NewConversationAsync()
    {
        // Save current conversation if it has messages
        if (Messages.Count > 0 && _agent is not null)
        {
            await SaveConversationAsync();
        }

        ClearConversation();
        IsHistorySheetOpen = false;
    }
}

/// <summary>
/// Represents a single chat message for display in the UI.
/// </summary>
public partial class ChatMessageViewModel(string content, bool isUser) : ObservableObject
{
    [ObservableProperty]
    private string _content = content;

    /// <summary>
    /// Gets a value indicating whether this message is from the user.
    /// </summary>
    public bool IsUser { get; } = isUser;

    /// <summary>
    /// Gets a value indicating whether this message is from the assistant.
    /// </summary>
    public bool IsAssistant => !IsUser;
}

/// <summary>
/// Represents a conversation history item for display in the history list.
/// </summary>
public sealed class ConversationHistoryItemViewModel(string id, string title, DateTimeOffset updatedAt)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public DateTimeOffset UpdatedAt { get; } = updatedAt;
    public string FormattedDate => UpdatedAt.LocalDateTime.ToString("dd MMM yyyy, HH:mm");
}
