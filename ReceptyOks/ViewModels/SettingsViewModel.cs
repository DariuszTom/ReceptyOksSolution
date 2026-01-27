using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Services;

namespace ReceptyOks.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly UpdateCheckerService _updateChecker;
    private readonly SyncService _syncService;

    [ObservableProperty]
    private string updateStatus;

    [ObservableProperty]
    private bool isChecking;

    public SettingsViewModel(UpdateCheckerService updateChecker, SyncService syncService)
    {
        _updateChecker = updateChecker;
        _syncService = syncService;
        UpdateStatus = string.Empty;
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        IsChecking = true;
        UpdateStatus = "Sprawdzanie...";
        var currentVersion = Microsoft.Maui.ApplicationModel.AppInfo.VersionString;
        var isUpdate = await _updateChecker.IsUpdateAvailableAsync(currentVersion);
        if (isUpdate)
        {
            var latest = await _updateChecker.GetLatestReleaseAsync();
            var apkAsset = latest?.Assets?.FirstOrDefault(a => a.Name != null && a.Name.EndsWith(".apk"));
            if (apkAsset != null)
            {
                UpdateStatus = $"Dostępna nowa wersja: {latest.TagName}";
                var result = await Shell.Current.DisplayAlertAsync(
                    "Nowa wersja dostępna",
                    $"Dostępna jest nowa wersja aplikacji ({latest.TagName}). Czy chcesz pobrać aktualizację?",
                    "Pobierz", "Anuluj");
                if (result)
                {
                    await Launcher.Default.OpenAsync(apkAsset.DownloadUrl);
                }
            }
            else
            {
                UpdateStatus = "Nie znaleziono pliku APK w najnowszym wydaniu.";
            }
        }
        else
        {
            UpdateStatus = "Masz najnowszą wersję.";
        }
        IsChecking = false;
    }
    [RelayCommand]
    public async Task ShowLogsAsync()
    {
        try
        {
            // Run navigation on the UI thread and use absolute route to ensure Shell finds the page
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("/LogsPage");
            });
        }
        catch (Exception ex)
        {
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlertAsync("Błąd nawigacji", $"Nie można otworzyć logów: {ex.Message}", "OK");
            });
        }
    }
    [RelayCommand]
    public async Task ForceUpdateBackendAsync()
    {
        var result = await _syncService.UploadAllAsync();
        if (result.Success)
        {
            await Shell.Current.DisplayAlertAsync("Synchronizacja", "Wymuszona aktualizacja zakończona pomyślnie.", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Synchronizacja", "Wymuszona aktualizacja nie powiodła się.", "OK");
        }
    }
}