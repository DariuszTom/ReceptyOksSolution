using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Data;
using ReceptyOks.Shared.AI;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for the chatbot page, managing conversation with the AI agent.
/// </summary>
public partial class ChatBotViewModel : ObservableObject
{
    private readonly AiAgent _agent;
    private readonly ILogger _logger;
    private readonly LocalDatabase _database;
    private CancellationTokenSource? _sendCts;
    private bool _toolsRegistered;

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

    /// <summary>
    /// Gets the collection of chat messages displayed in the UI.
    /// </summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public ChatBotViewModel(AiAgent aiAgent, LocalDatabase database, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(aiAgent);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = aiAgent;
        _logger = logger;
        _database = database;

        RegisterAgentTools();
    }

    private void RegisterAgentTools()
    {
        if (_toolsRegistered)
        {
            return;
        }

        _agent
            .AddTool<Task<string>>(
                GetAllRecipesAsync,
                "get_all_recipes",
                "Retrieves a list of all available recipes with their basic information (title, description, preparation time, cooking time, servings).")
            .AddToolAsync<string, string>(
                SearchRecipesAsync,
                "search_recipes",
                "Searches for recipes by text query matching title or description. Parameter: searchQuery - the text to search for.")
            .AddToolAsync<string, string>(
                GetRecipeDetailsAsync,
                "get_recipe_details",
                "Gets detailed information about a specific recipe including ingredients. Parameter: recipeId - the GUID of the recipe.")
            .AddTool<Task<string>>(
                GetAllCategoriesAsync,
                "get_all_categories",
                "Retrieves all recipe categories with their names and descriptions.")
            .AddToolAsync<string, string>(
                GetRecipesByCategoryAsync,
                "get_recipes_by_category",
                "Gets all recipes in a specific category. Parameter: categoryId - the GUID of the category.")
            .AddTool<Task<string>>(
                GetAllIngredientsAsync,
                "get_all_ingredients",
                "Retrieves a list of all available ingredients.");

        _toolsRegistered = true;
        _logger.Information("Registered {ToolCount} AI agent tools for database queries", _agent.Tools.Count);
    }

    private async Task<string> GetAllRecipesAsync()
    {
        var recipes = await _database.GetRecipesAsync().ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings,
            r.CategoryId
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> SearchRecipesAsync(string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return "[]";
        }

        var recipes = await _database.SearchRecipesAsync(searchQuery).ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetRecipeDetailsAsync(string recipeId)
    {
        if (!Guid.TryParse(recipeId, out var id))
        {
            return JsonSerializer.Serialize(new { error = "Invalid recipe ID format" });
        }

        var recipe = await _database.GetRecipeAsync(id).ConfigureAwait(false);
        if (recipe is null)
        {
            return JsonSerializer.Serialize(new { error = "Recipe not found" });
        }

        var recipeIngredients = await _database.GetRecipeIngredientsAsync(id).ConfigureAwait(false);
        var allIngredients = await _database.GetIngredientsAsync().ConfigureAwait(false);

        var ingredientDetails = recipeIngredients
            .Select(ri =>
            {
                var ingredient = allIngredients.FirstOrDefault(i => i.Id == ri.IngredientId);
                return new
                {
                    Name = ingredient?.Name ?? "Unknown",
                    ri.Quantity,
                    Unit = ingredient?.Unit
                };
            })
            .ToList();

        var result = new
        {
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Instructions,
            recipe.PreparationTimeMinutes,
            recipe.CookingTimeMinutes,
            recipe.Servings,
            recipe.CategoryId,
            Ingredients = ingredientDetails
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetAllCategoriesAsync()
    {
        var categories = await _database.GetCategoriesAsync().ConfigureAwait(false);
        var result = categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.Description
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetRecipesByCategoryAsync(string categoryId)
    {
        if (!Guid.TryParse(categoryId, out var id))
        {
            return JsonSerializer.Serialize(new { error = "Invalid category ID format" });
        }

        var recipes = await _database.GetRecipesByCategoryAsync(id).ConfigureAwait(false);
        var result = recipes.Select(r => new
        {
            r.Id,
            r.Title,
            r.Description,
            r.PreparationTimeMinutes,
            r.CookingTimeMinutes,
            r.Servings
        });
        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetAllIngredientsAsync()
    {
        var ingredients = await _database.GetIngredientsAsync().ConfigureAwait(false);
        var result = ingredients.Select(i => new
        {
            i.Id,
            i.Name,
            i.Unit
        });
        return JsonSerializer.Serialize(result);
    }

    private bool CanSendMessage => !string.IsNullOrWhiteSpace(UserInput) && !IsBusy;

    /// <summary>
    /// Sends the user's message to the AI agent and streams the response.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput))
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
            IsBusy = false;
            _sendCts?.Dispose();
            _sendCts = null;
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
        _agent.ClearHistory();
        HasError = false;
        ErrorMessage = string.Empty;
        _logger.Information("Conversation cleared");
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
