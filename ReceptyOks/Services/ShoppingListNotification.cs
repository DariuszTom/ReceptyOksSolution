using Microsoft.Extensions.Hosting;
using ReceptyOks.Configuration;

namespace ReceptyOks.Services;

/// <summary>
/// Background service that periodically checks for new shopping list items
/// and sends OS-level notifications when new ones are found.
/// </summary>
public sealed class ShoppingListNotification(
    IShoppingListService service,
    AppNotification notification,
    ILogger<ShoppingListNotification> logger,
    AppSettings appSettings,
    IPreferences preferences) : BackgroundService
{
    private readonly NotificationSettings _settings = appSettings.Notifications;
    private readonly IPreferences _preferences = preferences;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_settings.StartupDelay, stoppingToken).ConfigureAwait(false);

        await CheckForNewItemsAsync(stoppingToken).ConfigureAwait(false);

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(_settings.ShoppingListCheckIntervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckForNewItemsAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Shopping list notification service is stopping");
        }
    }

    private DateTime GetLastTimeCheck()
    {
        var ticks = _preferences.Get(_settings.PreferenceKey, DateTime.UtcNow.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private void SetLastTimeCheck(DateTime value)
    {
        _preferences?.Set(_settings.PreferenceKey, value.Ticks);
    }

    private async Task CheckForNewItemsAsync(CancellationToken cancellationToken)
    {
        var checkTime = DateTime.UtcNow;

        try
        {
            var result = await service.GetAllAsync(includeBought: false, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess || result.Data is null)
            {
                logger.LogWarning(
                    "Shopping list fetch failed: {Error}", result.ErrorMessage);
                return;
            }

            var previousCheck = GetLastTimeCheck();
            var newItems = result.Data
                .Where(item => item.CreatedAt > previousCheck)
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
