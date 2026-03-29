namespace ReceptyOks.Services;

/// <summary>
/// Event arguments for a received local notification.
/// </summary>
public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
