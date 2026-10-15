using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Transaction-price valuation of an apartment at a given location.
/// </summary>
public interface IPropertyValuationProvider
{
    /// <summary>
    /// Returns the valuation, or null when unavailable (no nearby transactions, service
    /// disabled or blocked, request failed). Never throws (except on cancellation).
    /// </summary>
    Task<RcnValuation?> GetApartmentValuationAsync(
        GeoPoint location, decimal areaSqm, CancellationToken cancellationToken = default);
}
