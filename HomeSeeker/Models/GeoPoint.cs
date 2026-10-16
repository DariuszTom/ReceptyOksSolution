using Geo;
using Geo.Geodesy;

namespace HomeSeeker.Models;

/// <summary>
/// WGS84 coordinates of a listing or place.
/// </summary>
/// <param name="Latitude">Latitude in degrees.</param>
/// <param name="Longitude">Longitude in degrees.</param>
/// <param name="IsApproximate">True when the portal only exposes an approximate location (e.g. OLX map radius).</param>
public readonly record struct GeoPoint(double Latitude, double Longitude, bool IsApproximate = false)
{
    private static readonly SpheroidCalculator Wgs84Calculator = new(Spheroid.Wgs84);

    public bool IsValid =>
        Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180 &&
        !(Latitude == 0 && Longitude == 0);

    /// <summary>
    /// Geodesic distance on the WGS84 ellipsoid in kilometres.
    /// </summary>
    public double DistanceKmTo(GeoPoint other)
    {
        var line = Wgs84Calculator.CalculateOrthodromicLine(
            new Coordinate(Latitude, Longitude),
            new Coordinate(other.Latitude, other.Longitude));

        // Geo returns null for coincident points.
        return line is null ? 0 : line.Distance.SiValue / 1000.0;
    }
}
