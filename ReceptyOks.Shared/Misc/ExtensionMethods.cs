
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
    }
}
