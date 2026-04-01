namespace ReceptyOks.Shared.Configuration
{
    public sealed record PeriodicSyncOptions
    {
        public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(30);
        public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);
        public bool ShowNotifications { get; init; } = false;
        public SyncType SyncType { get; init; } = SyncType.Normal;
    }
    public enum SyncType
    {
        Normal,
        Force,
        Manual
    }
}
