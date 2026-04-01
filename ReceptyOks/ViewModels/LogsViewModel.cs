using ReceptyOks.Services;

namespace ReceptyOks.ViewModels;

public partial class LogsViewModel(LocalDatabase database, ILogger<LogsViewModel> logger) : ObservableObject
{
    private readonly LocalDatabase _database = database;
    private readonly ILogger<LogsViewModel> _logger = logger;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _selectedLevel = "All";

    public static string AppVersion => VersionInfo.FormattedVersion;

    public List<string> LogLevels { get; } =
    [
        "All", "Debug", "Information", "Warning", "Error", "Fatal"
    ];

    [RelayCommand]
    private async Task LoadLogsAsync()
    {
        try
        {
            IsLoading = true;

            List<LogEntry> logs;
            if (SelectedLevel == "All")
            {
                logs = await _database.GetLogsAsync(200);
            }
            else
            {
                logs = await _database.GetLogsByLevelAsync(SelectedLevel, 200);
            }

            Logs.Clear();
            foreach (var log in logs)
            {
                Logs.Add(log);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading logs");
            await Shell.Current.DisplayAlertAsync("Error", "Failed to load logs", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearOldLogsAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Clear Old Logs",
            "This will delete logs older than 7 days. Continue?",
            "Yes",
            "No");

        if (!confirmed)
            return;

        try
        {
            IsLoading = true;
            var count = await _database.ClearOldLogsAsync(7);
            await LoadLogsAsync();
            await Shell.Current.DisplayAlertAsync("Success", $"Cleared {count} old logs", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing old logs");
            await Shell.Current.DisplayAlertAsync("Error", "Failed to clear logs", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearAllLogsAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Clear All Logs",
            "This will delete ALL logs. Continue?",
            "Yes",
            "No");

        if (!confirmed)
            return;

        try
        {
            IsLoading = true;
            var count = await _database.ClearAllLogsAsync();
            await LoadLogsAsync();
            await Shell.Current.DisplayAlertAsync("Success", "All logs cleared", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all logs");
            await Shell.Current.DisplayAlertAsync("Error", "Failed to clear logs", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedLevelChanged(string value)
    {
        Task.Run(async () => await LoadLogsAsync());
    }

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
