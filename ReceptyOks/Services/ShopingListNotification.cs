using Hangfire;
using Microsoft.Extensions.Logging;
using ReceptyOks.Configuration;

namespace ReceptyOks.Services;

internal sealed class ShopingListNotification
{
    private readonly ShoppingListService _service;
    private readonly AppNotification _notification;
    private readonly ILogger<ShopingListNotification> _logger;
    private readonly NotificationSettings _settings;

    public ShopingListNotification(
        ShoppingListService service,
        AppNotification notification,
        ILogger<ShopingListNotification> logger,
        AppSettings appSettings)
    {
        _service = service;
        _notification = notification;
        _logger = logger;
        _settings = appSettings.Notifications;
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

    public void ScheduleRecurringCheck()
    {
        var intervalMinutes = _settings.ShoppingListCheckIntervalMinutes;

        RecurringJob.AddOrUpdate(
            "shopping-list-new-items-check",
            () => CheckForNewItemsAsync(),
            $"*/{intervalMinutes} * * * *");

        _logger.LogInformation(
            "Scheduled shopping list check every {Interval} minutes", intervalMinutes);
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task CheckForNewItemsAsync()
    {
        var checkTime = DateTime.UtcNow;

        try
        {
            var result = await _service.GetAllAsync(includeBought: false).ConfigureAwait(false);

            if (!result.IsSuccess || result.Data is null)
            {
                _logger.LogWarning(
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

                _notification.SendNotification("Lista zakupów", message);
            }

            SetLastTimeCheck(checkTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for new shopping list items");
        }
    }
}
