using ReceptyOks.Misc;
using ReceptyOks.Shared.DTOs;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.Services
{
    public interface IShoppingListService
    {
        Task<ShoppingListResult<ShoppingListItem>> AddAsync(ShoppingListItem item, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<List<ShoppingListItem>>> AddBulkAsync(List<ShoppingListItem> items, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<BulkOperationResponse>> ClearAllAsync(CancellationToken cancellationToken = default);
        Task<ShoppingListResult<BulkOperationResponse>> ClearBoughtAsync(CancellationToken cancellationToken = default);
        Task<ShoppingListResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<List<ShoppingListItem>>> GetAllAsync(bool includeBought = false, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<ShoppingListItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<ShoppingListStats>> GetStatsAsync(CancellationToken cancellationToken = default);
        Task<ShoppingListResult<bool>> HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<ShoppingListItem>> MarkAsBoughtAsync(Guid id, string? boughtBy = null, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<ShoppingListItem>> MarkAsUnboughtAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<BulkOperationResponse>> MarkMultipleAsBoughtAsync(List<Guid> ids, string? boughtBy = null, CancellationToken cancellationToken = default);
        Task<ShoppingListResult<ShoppingListItem>> UpdateAsync(Guid id, ShoppingListItem item, CancellationToken cancellationToken = default);
    }
}