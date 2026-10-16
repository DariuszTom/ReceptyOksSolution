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

    Task<HomeSeekerResult<List<HomeSeekerScanRun>>> GetScansAsync(int count = 20, CancellationToken cancellationToken = default);

    Task<HomeSeekerResult<string>> GetReportHtmlAsync(Guid scanId, CancellationToken cancellationToken = default);
}

/// <summary>Obtains a fresh JWT when the stored one is missing or rejected by the server.</summary>
public interface IAccessTokenRefresher
{
    Task<bool> HasValidTokenAsync();

    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);
}
