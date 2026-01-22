using System;

namespace ReceptyOks.Shared.Misc
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Decodes the input string which can be either Base64 or hex into a byte array.
        /// </summary>
        public static byte[] DecodeBase64OrHexToBytes(this string input)
        {
            if (input == null) return Array.Empty<byte>();
            try
            {
                return Convert.FromBase64String(input);
            }
            catch
            {
                // Fallback to hex
                var hex = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input[2..] : input;
                return Convert.FromHexString(hex);
            }
        }
    }
}
