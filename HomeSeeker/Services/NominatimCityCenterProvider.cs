using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;

namespace HomeSeeker.Services;

/// <summary>
/// Resolves city centres with OpenStreetMap Nominatim. Results are cached for the process
/// lifetime; the HTTP client's resilience pipeline limits requests to one per second, as required
/// by the Nominatim usage policy.
/// </summary>
public sealed class NominatimCityCenterProvider : ICityCenterProvider
{
    public const string HttpClientName = "homeseeker-geo";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NominatimCityCenterProvider> _logger;
    private readonly ConcurrentDictionary<string, GeoPoint?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public NominatimCityCenterProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<NominatimCityCenterProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GeoPoint?> GetCenterAsync(string city, CancellationToken cancellationToken = default)
    {
        var key = city?.Trim();
        if (string.IsNullOrEmpty(key))
            return null;

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?city={Uri.EscapeDataString(key)}" +
                      "&countrycodes=pl&format=jsonv2&limit=1";
            using var response = await _httpClientFactory.CreateClient(HttpClientName)
                .GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim returned {Status} for city {City}", (int)response.StatusCode, key);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var point = ParseSearchResponse(json);
            // A definitive "not found" is cached too; transient failures are retried next time.
            _cache[key] = point;

            if (point is null)
                _logger.LogInformation("City centre not resolved for {City}; distances will be skipped", key);
            return point;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve city centre for {City}", key);
            return null;
        }
    }

    internal static GeoPoint? ParseSearchResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            if (!TryGetCoordinate(first, "lat", out var lat) || !TryGetCoordinate(first, "lon", out var lon))
                return null;

            var point = new GeoPoint(lat, lon);
            return point.IsValid ? point : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Nominatim returns coordinates as strings.
    private static bool TryGetCoordinate(JsonElement element, string name, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => prop.TryGetDouble(out value),
            _ => false
        };
    }
}
