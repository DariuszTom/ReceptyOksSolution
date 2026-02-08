global using System.Text;

namespace ReceptyOks.Shared
{
    public class GlobalConstants
    {
        public const string ApiKeyHeaderName = "X-Api-Key";
        public static readonly TimeSpan DefaultCancelationTokenTime = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan DefaultSnackBarTime = TimeSpan.FromSeconds(3);
    }
}