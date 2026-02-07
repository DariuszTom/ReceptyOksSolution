namespace ReceptyOks.Misc;

/// <summary>
/// Result wrapper for shopping list operations.
/// </summary>
/// <typeparam name="T">The type of the result data.</typeparam>
public class ShoppingListResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }

    private ShoppingListResult() { }

    public static ShoppingListResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ShoppingListResult<T> Failure(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
