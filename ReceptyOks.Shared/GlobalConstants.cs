namespace ReceptyOks.Shared;

public static class GlobalConstants
{
    public const string ApiKeyHeaderName = "X-Api-Key";
    public static readonly TimeSpan DefaultCancelationTokenTime = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DefaultSnackBarTime = TimeSpan.FromSeconds(3);
    public const int MaxRetryAttempts = 3;
    public static readonly TimeSpan AutoLoginValidationTimeout = TimeSpan.FromSeconds(15);
    public const int DefaultLogValueMaxLength = 256;
}
