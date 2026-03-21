namespace ReceptyOks.Shared.DTOs;

/// <summary>
/// Request to mark an item as bought.
/// </summary>
public record BoughtRequest(string? BoughtBy);

/// <summary>
/// Request to mark multiple items as bought.
/// </summary>
public record BulkBoughtRequest(List<Guid> Ids, string? BoughtBy);

/// <summary>
/// Response for bulk operations.
/// </summary>
public record BulkOperationResponse(int AffectedCount);

/// <summary>
/// Shopping list statistics.
/// </summary>
public record ShoppingListStats(int TotalItems, int BoughtItems, int PendingItems);
