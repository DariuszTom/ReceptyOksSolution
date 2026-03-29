using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReceptyOks.Configuration;
using System.Globalization;

namespace ReceptyOks.Services;

/// <summary>
/// Background service that periodically checks for new shopping list items
/// and sends OS-level notifications when new ones are found.
/// </summary>
internal sealed class ShopingListNotification(
    ShoppingListService service,
    AppNotification notification,
    ILogger<ShopingListNotification> logger,
    AppSettings appSettings) : BackgroundService
{
    private readonly NotificationSettings _settings = appSettings.Notifications;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_settings.StartupDelay, stoppingToken).ConfigureAwait(false);

        await CheckForNewItemsAsync().ConfigureAwait(false);

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(_settings.ShoppingListCheckIntervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckForNewItemsAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shopping list notification service is stopping");
        }
    }

    private DateTime GetLastTimeCheck()
    {
        var ticks = Preferences.Default.Get(_settings.PreferenceKey, DateTime.UtcNow.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private void SetLastTimeCheck(DateTime value)
    {
        Preferences.Default.Set(_settings.PreferenceKey, value.Ticks);
    }

    private async Task CheckForNewItemsAsync()
    {
        var checkTime = DateTime.UtcNow;

        try
        {
            var result = await service.GetAllAsync(includeBought: false).ConfigureAwait(false);

            if (!result.IsSuccess || result.Data is null)
            {
                logger.LogWarning(
                    "Shopping list fetch failed: {Error}", result.ErrorMessage);
                return;
            }

            string? userName = null;
            if (UserService.Instance.IsValueCreated)
            {
                var user = await UserService.Instance.Value.GetUserAsync().ConfigureAwait(false);
                userName = user?.Name;
            }
            
            var previousCheck = GetLastTimeCheck();
            var newItems = result.Data
                .Where(item => item.CreatedAt > previousCheck && userName != item.BoughtBy )
                .ToList();

            if (newItems.Count > 0)
            {
                var message = newItems.Count == 1
                    ? $"Nowy element na liście zakupów: {newItems[0].Name}"
                    : $"Nowe elementy na liście zakupów: {newItems.Count}";

                notification.SendNotification("Lista zakupów", message);
            }

            SetLastTimeCheck(checkTime);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for new shopping list items");
        }
    }
}
