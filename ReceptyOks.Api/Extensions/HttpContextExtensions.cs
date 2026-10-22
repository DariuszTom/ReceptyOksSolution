using System.Net;
using System.Net.Sockets;

namespace ReceptyOks.Api.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GetClientIp(this HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress;
            if (ip is null)
                return "unknown";

            if (ip.IsIPv4MappedToIPv6)
                return ip.MapToIPv4().ToString();

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // A single IPv6 client usually controls a whole /64, so partition by prefix to prevent address rotation bypass
                Span<byte> bytes = stackalloc byte[16];
                ip.TryWriteBytes(bytes, out _);
                bytes[8..].Clear();
                return $"{new IPAddress(bytes)}/64";
            }

            return ip.ToString();
        }
    }
}
