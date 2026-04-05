global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;

namespace ReceptyOks.Shared
{
    public class GlobalConstants
    {
        public const string ApiKeyHeaderName = "X-Api-Key";
        public static readonly TimeSpan DefaultCancelationTokenTime = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan DefaultSnackBarTime = TimeSpan.FromSeconds(3);
        public const int MaxRetryAttempts = 3;
    }
}