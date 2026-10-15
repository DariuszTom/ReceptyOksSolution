using ReceptyOks.Models.HomeSeeker;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReceptyOks.Services;

/// <summary>
/// Consumes <c>/api/homeseeker</c>. The typed <see cref="HttpClient"/> is configured with
/// <see cref="ApiKeyHandler"/> and <see cref="BearerTokenHandler"/>; when the JWT is missing or
/// rejected (401) the token is refreshed once and the request retried.
/// </summary>
public class HomeSeekerService(HttpClient httpClient, IAccessTokenRefresher tokenRefresher, ILogger<HomeSeekerService> logger) : IHomeSeekerService
{
    private const string BasePath = "/api/homeseeker";

    public async Task<HomeSeekerResult<HomeSeekerProfile>> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            ct => httpClient.GetAsync($"{BasePath}/profile", ct),
            (response, ct) => response.Content.ReadFromJsonAsync<HomeSeekerProfile>(ct),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<HomeSeekerResult<HomeSeekerProfile>> SaveProfileAsync(HomeSeekerProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ExecuteAsync(
            ct => httpClient.PutAsJsonAsync($"{BasePath}/profile", request, ct),
            (response, ct) => response.Content.ReadFromJsonAsync<HomeSeekerProfile>(ct),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<HomeSeekerResult<HomeSeekerPagedResult<HomeSeekerListing>>> GetListingsAsync(
        int pageNumber = 1, int pageSize = 20, int? minScore = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={pageNumber.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };
        if (minScore is { } score)
            query.Add($"minScore={score.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(sort))
            query.Add($"sort={Uri.EscapeDataString(sort)}");

        var url = $"{BasePath}/listings?{string.Join('&', query)}";

        return await ExecuteAsync(
            ct => httpClient.GetAsync(url, ct),
            (response, ct) => response.Content.ReadFromJsonAsync<HomeSeekerPagedResult<HomeSeekerListing>>(ct),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<HomeSeekerResult<List<HomeSeekerScanRun>>> GetScansAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}/scans?count={count.ToString(CultureInfo.InvariantCulture)}";

        return await ExecuteAsync(
            ct => httpClient.GetAsync(url, ct),
            async (response, ct) => await response.Content.ReadFromJsonAsync<List<HomeSeekerScanRun>>(ct).ConfigureAwait(false) ?? [],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<HomeSeekerResult<string>> GetReportHtmlAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            ct => httpClient.GetAsync($"{BasePath}/scans/{scanId}/report", ct),
            async (response, ct) => (string?)await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HomeSeekerResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, CancellationToken, Task<T?>> read,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await tokenRefresher.HasValidTokenAsync().ConfigureAwait(false))
            {
                await tokenRefresher.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }

            var response = await send(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized
                && await tokenRefresher.RefreshAsync(cancellationToken).ConfigureAwait(false))
            {
                response.Dispose();
                response = await send(cancellationToken).ConfigureAwait(false);
            }

            using (response)
            {
                return response.StatusCode switch
                {
                    _ when response.IsSuccessStatusCode =>
                        HomeSeekerResult<T>.Ok(await read(response, cancellationToken).ConfigureAwait(false)),
                    HttpStatusCode.NotFound =>
                        HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.NotFound, "Nie znaleziono"),
                    HttpStatusCode.BadRequest =>
                        HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.ValidationFailed, "Popraw błędy w formularzu",
                            await ReadValidationErrorsAsync(response, cancellationToken).ConfigureAwait(false)),
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.Unauthorized, "Brak autoryzacji – zaloguj się ponownie"),
                    HttpStatusCode.TooManyRequests =>
                        HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.Error, "Zbyt wiele zapytań – spróbuj za chwilę"),
                    _ => HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.Error, $"Błąd serwera: {(int)response.StatusCode}")
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "HomeSeeker API call failed");
            return HomeSeekerResult<T>.Fail(HomeSeekerCallStatus.Error, "Brak połączenia z serwerem");
        }
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("errors", out var errors)
                || errors.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string[]>();
            }

            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in errors.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                    ? property.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                    : [property.Value.ToString()];
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string[]>();
        }
    }
}
