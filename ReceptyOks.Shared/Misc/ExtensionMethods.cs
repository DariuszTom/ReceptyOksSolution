using System.Buffers;
using System.Security;

namespace ReceptyOks.Shared.Misc
{
    public static class ExtensionMethods
    {
        public const char LogReplacementChar = '_';

        // C0/C1 control chars (incl. CR/LF/TAB/ESC) plus Unicode line/paragraph separators that log viewers may render as new lines.
        private static readonly SearchValues<char> LogUnsafeChars = SearchValues.Create(
            "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" +
            "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F" +
            "\u007F\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u2028\u2029");

        /// <summary>
        /// Neutralizes user-controlled input before logging to prevent log forging/injection:
        /// control characters and line separators are replaced with <see cref="LogReplacementChar"/> and the value is truncated to <paramref name="maxLength"/>.
        /// Returns the original instance without allocating when the value is already safe.
        /// </summary>
        public static string SanitizeForLog(this string? value, int maxLength = GlobalConstants.DefaultLogValueMaxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

            var length = Math.Min(value.Length, maxLength);
            var firstUnsafe = value.AsSpan(0, length).IndexOfAny(LogUnsafeChars);

            if (firstUnsafe < 0)
            {
                return length == value.Length ? value : value[..length];
            }

            return string.Create(length, (value, firstUnsafe), static (span, state) =>
            {
                var source = state.value.AsSpan(0, span.Length);
                source.CopyTo(span);
                span[state.firstUnsafe..].ReplaceAny(LogUnsafeChars, LogReplacementChar);
            });
        }

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
        public static DateTime GetStartOfWeek(this DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }
        public static string GetPolishDayName(this DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Monday => "Poniedziałek",
            DayOfWeek.Tuesday => "Wtorek",
            DayOfWeek.Wednesday => "Środa",
            DayOfWeek.Thursday => "Czwartek",
            DayOfWeek.Friday => "Piątek",
            DayOfWeek.Saturday => "Sobota",
            DayOfWeek.Sunday => "Niedziela",
            _ => string.Empty
        };
        public static SecureString ToSecureString(this string str)
        {
            var secureString = new SecureString();
            for (int i = 0; i < str.Length; i++)
            {
                secureString.AppendChar(str[i]);
            }
            secureString.MakeReadOnly();
            return secureString;
        }
        public static string? Truncate(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }    
}
