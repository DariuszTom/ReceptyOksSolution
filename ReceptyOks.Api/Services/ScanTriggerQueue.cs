using System.Threading.Channels;

namespace ReceptyOks.Api.Services;

/// <summary>
/// Queue for triggering on-demand scans.
/// Uses Channel for thread-safe producer-consumer pattern.
/// </summary>
public sealed class ScanTriggerQueue
{
    private readonly Channel<Guid> _channel;

    public ScanTriggerQueue()
    {
        // Bounded channel to prevent unbounded memory growth
        // If the channel is full, TryWrite returns false
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    /// <summary>
    /// Queues a profile ID for scanning.
    /// </summary>
    /// <param name="profileId">The profile ID to scan.</param>
    /// <returns>True if queued successfully, false if queue is full.</returns>
    public bool TryQueueScan(Guid profileId)
    {
        return _channel.Writer.TryWrite(profileId);
    }

    /// <summary>
    /// Waits for the next scan request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profile ID to scan.</returns>
    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the reader for advanced consumption patterns.
    /// </summary>
    public ChannelReader<Guid> Reader => _channel.Reader;
}
