using Polly;
using Polly.Retry;
using ReceptyOks.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services;

public class UpdateCheckerService
{
    private readonly AppSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateCheckerService> _logger;

    public UpdateCheckerService(AppSettings settings, ILogger<UpdateCheckerService> logger)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        _settings = settings;
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri(_settings.Http.Github.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_settings.Http.DefaultTimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.Http.Github.UserAgent);
        _logger = logger;
    }

    public async Task<GitHubRelease?> GetLatestReleaseAsync()
    {
        AsyncRetryPolicy<HttpResponseMessage> retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, _, _, _) => outcome.Result?.Dispose());

        try
        {
            using var response = await retryPolicy.ExecuteAsync(() =>
                _httpClient.GetAsync(_settings.Http.Github.ReleaseEndpoint)
            ).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>().ConfigureAwait(false);
            return release;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsUpdateAvailableAsync(string apptVersion)
    {
        var latest = await GetLatestReleaseAsync().ConfigureAwait(false);
        if (latest == null)
            return false;

        string serverVersion = VersionInfo.ConvertVersionToNumeric(latest.TagName ?? string.Empty);
        string currentVersion = VersionInfo.ConvertVersionToNumeric(apptVersion);
        _logger.LogInformation("Checking for updates. Current version: {CurrentVersion}, Latest version: {LatestVersion}", currentVersion, serverVersion);
        // Porównanie wersji
        if (Version.TryParse(currentVersion, out var current) && Version.TryParse(serverVersion, out var latestVer))
        {
            return latestVer > current;
        }
        return false;
    }
    public async Task UpdateApp()
    {
        string currentVersion = AppInfo.VersionString; // MAUI: aktualna wersja aplikacji

        if (await IsUpdateAvailableAsync(currentVersion))
        {
            var latest = await GetLatestReleaseAsync();
            var apkAsset = latest?.Assets?.FirstOrDefault(a => a.Name != null && a.Name.EndsWith(".apk"));
            if (apkAsset != null)
            {
                // Wyświetl użytkownikowi informację i link do pobrania APK
                await Shell.Current.DisplayAlertAsync(
                    "Nowa wersja dostępna",
                    $"Dostępna jest nowa wersja aplikacji ({latest?.TagName}).{Environment.NewLine} Czy chcesz pobrać aktualizację?",
                    "Pobierz", "Anuluj");

                // Otwórz link do pobrania APK

                await Launcher.Default.OpenAsync(apkAsset.DownloadUrl);
            }
        }
    }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? DownloadUrl { get; set; }
}
