namespace ReceptyOks.ViewModels;

/// <summary>
/// Data point for memory usage history
/// </summary>
public partial class MemoryDataPoint : ObservableObject
{
    [ObservableProperty]
    private DateTime timestamp = DateTime.Now;

    [ObservableProperty]
    private decimal memoryMB;
}
