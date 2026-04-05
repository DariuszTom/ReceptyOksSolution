namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Shared state service for memory chart data between MAUI and Blazor
/// </summary>
public class MemoryChartState : BlazorContentState
{
    private readonly object _dataLock = new();
    private readonly List<MemoryDataPoint> _memoryData = [];
    private List<MemoryDataPoint>? _pendingData;
    private bool _pendingClear;

    public event Action? OnChange;

    public IReadOnlyList<MemoryDataPoint> MemoryData
    {
        get
        {
            lock (_dataLock)
            {
                return _memoryData.AsReadOnly();
            }
        }
    }

    public bool IsLoading { get; private set; }

    public decimal ThresholdMB { get; set; } = 400;

    public void SetLoading(bool isLoading)
    {
        IsLoading = isLoading;
        NotifyStateChanged();
    }

    public void AddDataPoint(DateTime timestamp, decimal memoryMB)
    {
        lock (_dataLock)
        {
            var newPoint = new MemoryDataPoint
            {
                Timestamp = timestamp,
                MemoryMB = memoryMB
            };

            if (!IsBlazorReady)
            {
                _pendingData ??= [.. _memoryData];
                _pendingData.Add(newPoint);

                // Keep only last 60 points in pending
                while (_pendingData.Count > 60)
                {
                    _pendingData.RemoveAt(0);
                }
                return;
            }

            _memoryData.Add(newPoint);

            // Keep only last 60 points
            while (_memoryData.Count > 60)
            {
                _memoryData.RemoveAt(0);
            }
        }

        NotifyStateChanged();
    }

    public void SetData(IEnumerable<MemoryDataPoint> data)
    {
        lock (_dataLock)
        {
            if (!IsBlazorReady)
            {
                _pendingData = data.ToList();
                _pendingClear = false;
                return;
            }

            _memoryData.Clear();
            _memoryData.AddRange(data);
        }

        NotifyStateChanged();
    }

    public void Clear()
    {
        lock (_dataLock)
        {
            if (!IsBlazorReady)
            {
                _pendingClear = true;
                _pendingData = null;
                return;
            }

            _memoryData.Clear();
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Called by the Blazor component when it is ready to receive data.
    /// Flushes any pending data that was queued before the component was ready.
    /// </summary>
    public void SignalChartReady()
    {
        bool shouldNotify = false;

        lock (_dataLock)
        {
            if (_pendingClear)
            {
                _memoryData.Clear();
                _pendingData = null;
                _pendingClear = false;
                shouldNotify = true;
            }
            else if (_pendingData is not null)
            {
                _memoryData.Clear();
                _memoryData.AddRange(_pendingData);
                _pendingData = null;
                shouldNotify = true;
            }
        }

        // Call base to mark Blazor as ready
        SignalReady();

        if (shouldNotify)
        {
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Pauses delivery when the Blazor component is disposed.
    /// Preserves current data so the next component instance receives it.
    /// </summary>
    public void PauseChart()
    {
        lock (_dataLock)
        {
            _pendingData ??= [.. _memoryData];
        }

        Pause();
    }

    /// <summary>
    /// Fully resets chart state including data.
    /// </summary>
    public void ResetChart()
    {
        lock (_dataLock)
        {
            _memoryData.Clear();
            _pendingData = null;
            _pendingClear = false;
        }

        Reset();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public class MemoryDataPoint
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public decimal MemoryMB { get; set; }
    }
}
