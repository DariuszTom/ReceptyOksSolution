namespace ReceptyOks.Shared
{
    public enum Jednostki
    {
        Brak=0,
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
}
