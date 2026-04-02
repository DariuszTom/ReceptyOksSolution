using ReceptyOks.Interfaces;
using ReceptyOks.Models;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for displaying backend health status
/// </summary>
public partial class AppStatusViewModel : ObservableObject
{
    private readonly IHealthStatusService _healthStatusService;
    private readonly ILogger<AppStatusViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isHealthy;

    [ObservableProperty]
    private bool isDegraded;

    [ObservableProperty]
    private bool isUnhealthy;

    [ObservableProperty]
    private string statusText = "Nieznany";

    [ObservableProperty]
    private string statusIcon = "●";

    [ObservableProperty]
    private Color statusColor = Colors.Gray;

    [ObservableProperty]
    private string totalDuration = "-";

    [ObservableProperty]
    private string lastChecked = "-";

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ObservableCollection<HealthCheckItemViewModel> healthChecks = [];

    public AppStatusViewModel(IHealthStatusService healthStatusService, ILogger<AppStatusViewModel> logger)
    {
        _healthStatusService = healthStatusService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task CheckHealthAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Run both health and alive checks
            var healthTask = _healthStatusService.GetHealthStatusAsync();
            var aliveTask = _healthStatusService.IsAliveAsync();

            await Task.WhenAll(healthTask, aliveTask).ConfigureAwait(false);

            var response = await healthTask;
            var isAlive = await aliveTask;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateFromResponse(response);

                // Add alive status to entries if not already present
                if (isAlive && !HealthChecks.Any(h => h.Name == "Liveness"))
                {
                    HealthChecks.Insert(0, new HealthCheckItemViewModel
                    {
                        Name = "Liveness",
                        Status = "Healthy",
                        Duration = "0 ms",
                        StatusColor = Colors.Green,
                        Description = "Serwer odpowiada na /alive",
                        Tags = "live"
                    });
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Cancelled, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check health status");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ErrorMessage = $"Błąd: {ex.Message}";
                SetUnhealthyState();
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateFromResponse(HealthStatusResponse response)
    {
        LastChecked = response.CheckedAt.ToLocalTime().ToString("HH:mm:ss");

        if (!response.IsSuccess)
        {
            ErrorMessage = response.ErrorMessage;
            SetUnhealthyState();
            return;
        }

        IsHealthy = response.IsHealthy;
        IsDegraded = response.IsDegraded;
        IsUnhealthy = response.IsUnhealthy;

        StatusText = response.Status switch
        {
            "Healthy" => "Zdrowy",
            "Degraded" => "Obniżona wydajność",
            "Unhealthy" => "Niezdatny",
            _ => response.Status
        };

        StatusColor = response.Status switch
        {
            "Healthy" => Colors.Green,
            "Degraded" => Colors.Orange,
            "Unhealthy" => Colors.Red,
            _ => Colors.Gray
        };

        StatusIcon = response.Status switch
        {
            "Healthy" => "✓",
            "Degraded" => "⚠",
            "Unhealthy" => "✗",
            _ => "●"
        };

        TotalDuration = $"{response.TotalDuration.TotalMilliseconds:F0} ms";

        HealthChecks.Clear();
        foreach (var entry in response.Entries)
        {
            HealthChecks.Add(new HealthCheckItemViewModel
            {
                Name = GetFriendlyName(entry.Key),
                Status = entry.Value.Status,
                Duration = $"{entry.Value.Duration.TotalMilliseconds:F0} ms",
                StatusColor = entry.Value.Status switch
                {
                    "Healthy" => Colors.Green,
                    "Degraded" => Colors.Orange,
                    "Unhealthy" => Colors.Red,
                    _ => Colors.Gray
                },
                Description = entry.Value.Description,
                Tags = string.Join(", ", entry.Value.Tags)
            });
        }
    }

    private void SetUnhealthyState()
    {
        IsHealthy = false;
        IsDegraded = false;
        IsUnhealthy = true;
        StatusText = "Niezdatny";
        StatusColor = Colors.Red;
        StatusIcon = "✗";
    }

    private static string GetFriendlyName(string key) => key switch
    {
        "memory" => "Pamięć",
        "database" => "Baza danych",
        "self" => "Aplikacja",
        _ => key
    };
    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
