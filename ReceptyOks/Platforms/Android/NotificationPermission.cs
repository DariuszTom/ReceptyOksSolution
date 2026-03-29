using Android;

namespace ReceptyOks.Platforms.Android;

/// <summary>
/// Runtime permission check for POST_NOTIFICATIONS (required on Android 13+).
/// </summary>
public class NotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            var result = new List<(string androidPermission, bool isRuntime)>();
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                result.Add((Manifest.Permission.PostNotifications, true));
            return [.. result];
        }
    }
}
