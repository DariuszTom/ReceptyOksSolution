using HomeSeeker.Models;

namespace HomeSeeker.Abstractions;

/// <summary>
/// Interface for AI-based listing evaluation.
/// </summary>
public interface IListingEvaluator
{
    /// <summary>
    /// Evaluates a listing using AI, fetching the listing page details.
    /// Returns null on evaluation failure (bad JSON, timeout, etc.) - never throws.
    /// </summary>
    /// <param name="profile">Search profile with criteria and preferences.</param>
    /// <param name="listing">The listing to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation result, or null on failure.</returns>
    Task<ListingEvaluation?> EvaluateAsync(
        SearchProfile profile,
        HouseListing listing,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an HTML report for the top listings.
    /// Falls back to a code-generated table if AI generation fails.
    /// </summary>
    /// <param name="profile">Search profile.</param>
    /// <param name="listings">Top listings to include in the report.</param>
    /// <param name="scanRun">Scan run metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTML report string.</returns>
    Task<string> WriteReportHtmlAsync(
        SearchProfile profile,
        IReadOnlyList<HouseListing> listings,
        ScanRun scanRun,
        CancellationToken cancellationToken = default);
}
