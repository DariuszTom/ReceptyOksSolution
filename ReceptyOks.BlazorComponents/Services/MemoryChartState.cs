namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Shared state service for memory chart data between MAUI and Blazor
/// </summary>
public class MemoryChartState: BlazorContentState
{
    private readonly List<MemoryDataPoint> _memoryData = [];

    public event Action? OnChange;

    public IReadOnlyList<MemoryDataPoint> MemoryData => _memoryData.AsReadOnly();

    public bool IsLoading { get; private set; }

    public decimal ThresholdMB { get; set; } = 400;

    public void SetLoading(bool isLoading)
    {
        IsLoading = isLoading;
        NotifyStateChanged();
    }

    public void AddDataPoint(DateTime timestamp, decimal memoryMB)
    {
        _memoryData.Add(new MemoryDataPoint
        {
            Timestamp = timestamp,
            MemoryMB = memoryMB
        });

        // Keep only last 60 points
        while (_memoryData.Count > 60)
        {
            _memoryData.RemoveAt(0);
        }

        NotifyStateChanged();
    }

    public void SetData(IEnumerable<MemoryDataPoint> data)
    {
        _memoryData.Clear();
        _memoryData.AddRange(data);
        NotifyStateChanged();
    }

    public void Clear()
    {
        _memoryData.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public class MemoryDataPoint
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public decimal MemoryMB { get; set; }
    }
}
