namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for individual health check entry
/// </summary>
public partial class HealthCheckItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string status = string.Empty;

    [ObservableProperty]
    private string duration = string.Empty;

    [ObservableProperty]
    private Color statusColor = Colors.Gray;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? tags;
}
