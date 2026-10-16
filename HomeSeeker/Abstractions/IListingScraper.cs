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
    /// Implementations should not throw for request/parsing issues; instead they
    /// log and return <see cref="ScrapeResult.Failure"/> so the caller can tell a
    /// broken portal apart from a genuinely empty result set.
    /// </summary>
    /// <param name="profile">Search profile with criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ScrapeResult> SearchAsync(SearchProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the listing page and returns its parameters and description as plain,
    /// length-capped text for AI evaluation, plus its coordinates when available. Only URLs
    /// on the scraper's own portal are fetched. Returns null on any failure - never throws
    /// (except on cancellation).
    /// </summary>
    /// <param name="url">Listing URL (must belong to this portal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ListingDetails?> GetDetailsAsync(string url, CancellationToken cancellationToken = default);
}
