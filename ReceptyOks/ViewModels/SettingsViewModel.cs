using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Services;

namespace ReceptyOks.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly UpdateCheckerService _updateChecker;

    [ObservableProperty]
    private string updateStatus;

    [ObservableProperty]
    private bool isChecking;

    public SettingsViewModel(UpdateCheckerService updateChecker)
    {
        _updateChecker = updateChecker;
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
}