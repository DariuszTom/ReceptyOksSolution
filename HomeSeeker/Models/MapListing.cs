namespace HomeSeeker.Models;

/// <summary>
/// Lightweight listing shown as a map pin.
/// </summary>
public sealed record MapListing(
    Guid Id,
    string Portal,
    string Url,
    string Title,
    decimal Price,
    decimal? PreviousPrice,
    decimal AreaSqm,
    string? Location,
    PropertyTypes PropertyType,
    int? AiScore,
    string? AiSummary,
    double Latitude,
    double Longitude,
    bool CoordinatesApproximate,
    double? DistanceFromCenterKm,
    bool DistanceIsApproximate)
{
    /// <summary>
    /// Maps a listing that has coordinates.
    /// </summary>
    public static MapListing From(HouseListing listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        return new MapListing(
            listing.Id,
            listing.Portal,
            listing.Url,
            listing.Title,
            listing.Price,
            listing.PreviousPrice,
            listing.AreaSqm,
            listing.Location,
            listing.PropertyType,
            listing.AiScore,
            listing.AiSummary,
            listing.Latitude ?? 0,
            listing.Longitude ?? 0,
            listing.CoordinatesApproximate,
            listing.DistanceFromCenterKm,
            listing.DistanceIsApproximate);
    }
}
