using ReceptyOks.Models.HomeSeeker;

namespace ReceptyOks.Services;

/// <summary>
/// Client for the owner-scoped HomeSeeker API (<c>/api/homeseeker</c>). Requests carry the shared
/// API key and the JWT issued at login.
/// </summary>
public interface IHomeSeekerService
{
    /// <summary>Returns <see cref="HomeSeekerCallStatus.NotFound"/> when the shared profile has not been configured yet.</summary>
    Task<HomeSeekerResult<HomeSeekerProfile>> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<HomeSeekerResult<HomeSeekerProfile>> SaveProfileAsync(HomeSeekerProfileRequest request, CancellationToken cancellationToken = default);

    Task<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>> GetListingsAsync(
        int pageNumber, int pageSize, HomeSeekerListingQuery query, CancellationToken cancellationToken = default);

    /// <summary>Best scored listings with coordinates (filters as in <paramref name="query"/>; sort is ignored).</summary>
    Task<HomeSeekerResult<List<HomeSeekerMapListing>>> GetMapListingsAsync(
        int limit, HomeSeekerListingQuery query, CancellationToken cancellationToken = default);

    /// <summary>Names of the portals scanned by the server (values of the listing portal filter).</summary>
    Task<HomeSeekerResult<List<string>>> GetPortalsAsync(CancellationToken cancellationToken = default);

    Task<HomeSeekerResult<List<HomeSeekerScanRun>>> GetScansAsync(int count = 20, CancellationToken cancellationToken = default);

    Task<HomeSeekerResult<string>> GetReportHtmlAsync(Guid scanId, CancellationToken cancellationToken = default);
}

/// <summary>Obtains a fresh JWT when the stored one is missing or rejected by the server.</summary>
public interface IAccessTokenRefresher
{
    Task<bool> HasValidTokenAsync();

    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);
}
