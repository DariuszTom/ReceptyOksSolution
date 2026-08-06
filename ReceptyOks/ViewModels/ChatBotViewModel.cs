using Microsoft.Extensions.AI;
using ReceptyOks.Services;
using ReceptyOks.Shared;
using ReceptyOks.Shared.AI;
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
    private readonly AttachmentService _attachmentService;
    private AiAgent? _agent;
    private CancellationTokenSource? _sendCts;
    private string? _currentConversationId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _userInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingAttachment))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveAttachmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowAttachmentOptionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakePhotoCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickDocumentCommand))]
    private ChatAttachment? _pendingAttachment;

    /// <summary>
    /// True when the user has picked an attachment but hasn't sent it yet.
    /// </summary>
    public bool HasPendingAttachment => PendingAttachment is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowAttachmentOptionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakePhotoCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickDocumentCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowAttachmentOptionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakePhotoCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickDocumentCommand))]
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

    public ChatBotViewModel(
        LocalDatabase database,
        TokenProviderService tokenProvider,
        AttachmentService attachmentService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(attachmentService);
        ArgumentNullException.ThrowIfNull(logger);

        _database = database;
        _logger = logger;
        _toolsRegistrar = new AgentToolsRegistrar(database, logger);
        _tokenProvider = tokenProvider;
        _attachmentService = attachmentService;
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
                string prompt = settings.SystemPrompt;
                if (await UserService.Instance.Value.HasUserAsync())
                {
                    var user = await UserService.Instance.Value.GetUserAsync();
                    prompt = prompt.Replace("{UserName}", user.Name ?? "Nie podano");
                }
                _agent = new AiAgent(anthritopicAgent.GetAgent(), prompt);
            }

            // Register tools
            _toolsRegistrar.RegisterTools(_agent);

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

    private bool CanSendMessage =>
        (!string.IsNullOrWhiteSpace(UserInput) || PendingAttachment is not null)
        && !IsBusy
        && !IsInitializing
        && _agent is not null;

    /// <summary>
    /// Sends the user's message to the AI agent and streams the response.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (_agent is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(UserInput) && PendingAttachment is null)
        {
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;

        var userText = UserInput.Trim();
        UserInput = string.Empty;

        // Snapshot and clear the pending attachment so the UI resets immediately.
        var attachment = PendingAttachment;
        PendingAttachment = null;

        // Add user message to UI (including attachment metadata for thumbnail display)
        Messages.Add(new ChatMessageViewModel(userText, isUser: true)
        {
            AttachmentPath = attachment?.FilePath,
            AttachmentMediaType = attachment?.MediaType,
            AttachmentFileName = attachment?.OriginalFileName,
        });

        // Add placeholder for assistant response
        var assistantMessage = new ChatMessageViewModel(string.Empty, isUser: false);
        Messages.Add(assistantMessage);

        IsBusy = true;
        _sendCts = new CancellationTokenSource(GlobalConstants.DefaultCancelationTokenTime * 3);

        try
        {
            if (attachment is not null)
            {
                if (attachment.IsTextDocument)
                {
                    // Text document: inline extracted content into the prompt as fenced context.
                    var instruction = string.IsNullOrWhiteSpace(userText)
                        ? "Przeanalizuj załączony dokument."
                        : userText;

                    var promptText =
                        $"{instruction}\n\n" +
                        $"--- Zawartość pliku \"{attachment.OriginalFileName}\" ---\n" +
                        $"{attachment.TextContent}\n" +
                        $"--- Koniec pliku ---";

                    await _agent.ChatStreamAsync(
                        promptText,
                        chunk => MainThread.BeginInvokeOnMainThread(() => assistantMessage.Content += chunk),
                        _sendCts.Token).ConfigureAwait(false);
                }
                else
                {
                    // Multimodal: text + binary attachment (image or PDF).
                    var promptText = string.IsNullOrWhiteSpace(userText)
                        ? (attachment.IsPdf
                            ? "Przeanalizuj załączony dokument PDF."
                            : "Przeanalizuj załączony obraz.")
                        : userText;

                    var chatMessage = new ChatMessage(ChatRole.User,
                    [
                        new TextContent(promptText),
                        new DataContent(attachment.Data, attachment.MediaType),
                    ]);

                    await _agent.ChatStreamAsync(
                        chatMessage,
                        chunk => MainThread.BeginInvokeOnMainThread(() => assistantMessage.Content += chunk),
                        _sendCts.Token).ConfigureAwait(false);
                }
            }
            else
            {
                await _agent.ChatStreamAsync(
                    userText,
                    chunk => MainThread.BeginInvokeOnMainThread(() => assistantMessage.Content += chunk),
                    _sendCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
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

    private bool CanPickAttachment => !IsBusy && !IsInitializing && PendingAttachment is null;

    /// <summary>
    /// Shows an action sheet letting the user choose how to attach a file
    /// (gallery, camera, PDF, or document). Replaces any previously pending attachment.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPickAttachment))]
    private async Task ShowAttachmentOptionsAsync()
    {
        _logger.Information("ShowAttachmentOptionsAsync invoked (IsBusy={IsBusy}, IsInitializing={IsInit}, HasPending={HasPending})",
            IsBusy, IsInitializing, PendingAttachment is not null);

        var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
        if (page is null)
        {
            _logger.Warning("Cannot show attachment options: no active page");
            return;
        }

        string? choice;
        try
        {
            // RelayCommand handlers already run on the UI thread in .NET MAUI, so no
            // MainThread marshalling is required here.
            choice = await page.DisplayActionSheetAsync(
                "Dodaj załącznik",
                "Anuluj",
                null,
                FlowDirection.MatchParent,
                "Zrób zdjęcie",
                "Wybierz z galerii",
                "Wybierz plik PDF",
                "Wybierz dokument (TXT, DOCX, ...)");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to display attachment action sheet");
            return;
        }

        _logger.Information("Attachment action sheet result: {Choice}", choice ?? "<null>");

        if (string.IsNullOrEmpty(choice) || choice == "Anuluj")
        {
            return;
        }

        switch (choice)
        {
            case "Zrób zdjęcie":
                await TakePhotoAsync().ConfigureAwait(false);
                break;
            case "Wybierz z galerii":
                await PickImageAsync().ConfigureAwait(false);
                break;
            case "Wybierz plik PDF":
                await PickPdfAsync().ConfigureAwait(false);
                break;
            case "Wybierz dokument (TXT, DOCX, ...)":
                await PickDocumentAsync().ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Picks a photo from the device gallery.</summary>
    [RelayCommand(CanExecute = nameof(CanPickAttachment))]
    private async Task PickImageAsync()
    {
        var attachment = await _attachmentService.PickImageAsync().ConfigureAwait(false);
        await ApplyPickedAttachmentAsync(attachment).ConfigureAwait(false);
    }

    /// <summary>Captures a photo with the device camera.</summary>
    [RelayCommand(CanExecute = nameof(CanPickAttachment))]
    private async Task TakePhotoAsync()
    {
        var attachment = await _attachmentService.CapturePhotoAsync().ConfigureAwait(false);
        await ApplyPickedAttachmentAsync(attachment).ConfigureAwait(false);
    }

    /// <summary>Picks a PDF document from the file system.</summary>
    [RelayCommand(CanExecute = nameof(CanPickAttachment))]
    private async Task PickPdfAsync()
    {
        var attachment = await _attachmentService.PickPdfAsync().ConfigureAwait(false);
        await ApplyPickedAttachmentAsync(attachment).ConfigureAwait(false);
    }

    /// <summary>Picks a text-based document (TXT, MD, CSV, JSON, DOCX, ...) from the file system.</summary>
    [RelayCommand(CanExecute = nameof(CanPickAttachment))]
    private async Task PickDocumentAsync()
    {
        var attachment = await _attachmentService.PickDocumentAsync().ConfigureAwait(false);
        await ApplyPickedAttachmentAsync(attachment).ConfigureAwait(false);
    }

    private bool CanRemoveAttachment => PendingAttachment is not null;

    /// <summary>Removes the currently pending attachment before the message is sent.</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveAttachment))]
    private void RemoveAttachment()
    {
        var attachment = PendingAttachment;
        PendingAttachment = null;

        // Best-effort cleanup of the on-disk copy (message hasn't been sent yet).
        if (attachment is not null)
        {
            _attachmentService.DeleteAttachment(attachment.FilePath);
        }
    }

    private Task ApplyPickedAttachmentAsync(ChatAttachment? attachment)
    {
        if (attachment is null)
        {
            return Task.CompletedTask;
        }

        return MainThread.InvokeOnMainThreadAsync(() => PendingAttachment = attachment);
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

        // Preserve a pending (not-yet-sent) attachment so the user does not lose it when
        // they clear the conversation mid-compose.
        var keep = PendingAttachment?.FilePath is { Length: > 0 } path
            ? new[] { path }
            : Array.Empty<string>();
        _attachmentService.CleanupOrphanAttachments(keep);
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

            // Extract and display conversation history in UI. Attachments (image/PDF) embedded in the
            // saved session are rematerialised onto disk so that thumbnails render correctly.
            var history = ConversationHistoryParser.Parse(conversation.SerializedThread);

            var viewModels = new List<ChatMessageViewModel>(history.Count);
            foreach (var message in history)
            {
                ChatAttachment? restored = null;
                if (message.AttachmentBytes is { Length: > 0 } bytes &&
                    !string.IsNullOrEmpty(message.AttachmentMediaType))
                {
                    try
                    {
                        restored = await _attachmentService
                            .MaterializeAsync(bytes, message.AttachmentMediaType, message.AttachmentFileName)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to materialize attachment from history");
                    }
                }

                viewModels.Add(new ChatMessageViewModel(message.Content, message.IsUser)
                {
                    AttachmentPath = restored?.FilePath,
                    AttachmentMediaType = restored?.MediaType ?? message.AttachmentMediaType,
                    AttachmentFileName = restored?.OriginalFileName ?? message.AttachmentFileName,
                });
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
                     {
                         Messages.Clear();

                         foreach (var vm in viewModels)
                         {
                             Messages.Add(vm);
                         }

                         if (viewModels.Count == 0)
                         {
                             Messages.Add(new ChatMessageViewModel($"[Załadowano rozmowę: {item.Title}]", isUser: false));
                         }
                     });

            // Remove attachment files that no longer belong to the newly loaded conversation.
            var keepPaths = viewModels
                .Select(vm => vm.AttachmentPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Cast<string>()
                .ToArray();
            _attachmentService.CleanupOrphanAttachments(keepPaths);
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

    /// <summary>Absolute path to the persisted attachment file, if any.</summary>
    public string? AttachmentPath { get; init; }

    /// <summary>MIME type of the attachment (e.g. image/jpeg, application/pdf).</summary>
    public string? AttachmentMediaType { get; init; }

    /// <summary>Original file name shown to the user.</summary>
    public string? AttachmentFileName { get; init; }

    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath);

    public bool HasImageAttachment =>
        HasAttachment && (AttachmentMediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);

    public bool HasPdfAttachment =>
        HasAttachment && string.Equals(AttachmentMediaType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the attachment is a text-based document (TXT/MD/CSV/DOCX/...)—not an image nor a PDF.</summary>
    public bool HasDocumentAttachment => HasAttachment && !HasImageAttachment && !HasPdfAttachment;
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
