using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReceptyOks.Data;
using ReceptyOks.Services;
using System.Collections.ObjectModel;

namespace ReceptyOks.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly LocalDatabase _database;
    private readonly ILogger<LogsViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _selectedLevel = "All";

    public string AppVersion => VersionInfo.FormattedVersion;

    public List<string> LogLevels { get; } = new() 
    { 
        "All", "Debug", "Information", "Warning", "Error", "Fatal" 
    };

    public LogsViewModel(LocalDatabase database, ILogger<LogsViewModel> logger)
    {
        _database = database;
        _logger = logger;
    }

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

            _logger.LogInformation("Loaded {Count} log entries", logs.Count);
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
            _logger.LogInformation("Cleared {Count} old log entries", count);
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
            _logger.LogInformation("Cleared all logs ({Count} entries)", count);
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
}
