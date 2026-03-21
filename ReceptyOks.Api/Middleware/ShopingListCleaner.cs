using Microsoft.EntityFrameworkCore;
using ReceptyOks.Shared.Configuration;

namespace ReceptyOks.Api.Middleware;

/// <summary>
/// Periodically hard-deletes shopping list items that were bought more than
/// <see cref="CleanupOptions.MaxAge"/> ago. Runs on a recurring interval after a startup delay.
/// </summary>
public sealed class ShopingListCleaner(
    IServiceScopeFactory scopeFactory,
    CleanupOptions options,
    ILogger<ShopingListCleaner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);

        await CleanupAsync(stoppingToken);

        using PeriodicTimer timer = new(options.Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shopping list cleanup service is stopping");
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();

            var cutoff = DateTime.UtcNow - options.MaxAge;

            var deleted = await db.ShoppingListItems
                .Where(s => s.IsBought && s.BoughtAt != null && s.BoughtAt < cutoff)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} bought shopping list items older than {Days} days",
                    deleted, options.MaxAge.Days);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shopping list cleanup failed");
        }
    }
}
