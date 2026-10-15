namespace HomeSeeker.Models;

/// <summary>
/// Kinds of property a search profile scans for. Flags, so a profile can ask for both;
/// a single listing always carries exactly one value.
/// </summary>
[Flags]
public enum PropertyTypes
{
    None = 0,
    House = 1,
    Apartment = 2,
    All = House | Apartment
}

public static class PropertyTypesExtensions
{
    /// <summary>Selected single types in a stable order (houses first).</summary>
    public static IEnumerable<PropertyTypes> Selected(this PropertyTypes types)
    {
        if (types.HasFlag(PropertyTypes.House))
            yield return PropertyTypes.House;
        if (types.HasFlag(PropertyTypes.Apartment))
            yield return PropertyTypes.Apartment;
    }

    public static string ToPolish(this PropertyTypes type) => type switch
    {
        PropertyTypes.House => "dom",
        PropertyTypes.Apartment => "mieszkanie",
        PropertyTypes.All => "dom lub mieszkanie",
        _ => "nieruchomość"
    };
}
