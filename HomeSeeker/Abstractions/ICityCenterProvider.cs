using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Resolves a city's centre coordinates (used to measure how far a listing is from the centre).
/// </summary>
public interface ICityCenterProvider
{
    /// <summary>
    /// Returns the city centre, or null when it can't be resolved. Never throws (except on cancellation).
    /// </summary>
    Task<GeoPoint?> GetCenterAsync(string city, CancellationToken cancellationToken = default);
}
