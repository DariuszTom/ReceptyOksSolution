using System.Runtime.CompilerServices;

namespace ReceptyOks.Shared
{
    public enum Units
    {
        Brak = 0,
        Sztuka,
        Gram,
        Kilogram,
        Mililitr,
        Litr,
        Lyzeczka,
        Lyzka,
        Szklanka,
        Opakowanie,
        Zabek,
        Garsc,
        Szczypta
    }

    public static class EnumHelpers
    {
        public static List<T> ToList<T>() where T : Enum
            => Enum.GetValues(typeof(T)).Cast<T>().ToList();
    }

    public static class UnitsExtensions
    {
        /// <summary>
        /// Determines if the unit represents a countable item that should be rounded up.
        /// </summary>
        /// <param name="unit">The unit to check.</param>
        /// <returns>True if the unit is countable (e.g., pieces, cloves), false for measurable units.</returns>
        public static bool IsCountable(this Units unit) => unit switch
        {
            Units.Sztuka => true,
            Units.Opakowanie => true,
            Units.Zabek => true,
            _ => false
        };
        public static Units Parse(string value)
        {
            if (Enum.TryParse<Units>(value, true, out var result))
            {
                return result;
            }
            return Units.Brak; // Default value if parsing fails
        }
    }
}
