using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Interface for portal-specific listing scrapers.
/// </summary>
public interface IListingScraper
{
    /// <summary>
    /// Portal name (e.g., "Otodom", "OLX").
    /// </summary>
    string PortalName { get; }

    /// <summary>
    /// Searches for listings matching the profile criteria.
    /// Implementations should not throw exceptions for content/parsing issues - log and return empty.
    /// </summary>
    /// <param name="profile">Search profile with criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of scraped listings.</returns>
    Task<IReadOnlyList<ScrapedListing>> SearchAsync(SearchProfile profile, CancellationToken cancellationToken = default);
}
