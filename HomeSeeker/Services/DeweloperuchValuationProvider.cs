using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using HomeSeeker.Abstractions;
using HomeSeeker.Configuration;
using HomeSeeker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeSeeker.Services;

/// <summary>
/// Apartment valuation from deweloperuch.pl, based on notarial transaction prices (RCN)
/// near the given point. Used sparingly: only for AI candidates, at most one request per
/// <see cref="HomeSeekerOptions.RcnValuationRequestInterval"/> (enforced by the HTTP client's
/// resilience pipeline), cached, and switched off for
/// <see cref="HomeSeekerOptions.RcnValuationBlockedCooldown"/> after the service refuses access.
/// </summary>
public sealed class DeweloperuchValuationProvider : IPropertyValuationProvider
{
    public const string HttpClientName = "homeseeker-valuation";

    // Limits enforced by the service; requests outside them are pointless.
    private const double MinLat = 49.0, MaxLat = 54.9, MinLng = 14.1, MaxLng = 24.2;
    private const decimal MinArea = 10, MaxArea = 500;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HomeSeekerOptions _options;
    private readonly ILogger<DeweloperuchValuationProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, RcnValuation?> _cache = new();
    private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public DeweloperuchValuationProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<HomeSeekerOptions> options,
        ILogger<DeweloperuchValuationProvider> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RcnValuation?> GetApartmentValuationAsync(
        GeoPoint location, decimal areaSqm, CancellationToken cancellationToken = default)
    {
        if (!_options.UseRcnValuation || IsBlocked())
            return null;

        if (location.Latitude is < MinLat or > MaxLat || location.Longitude is < MinLng or > MaxLng)
            return null;

        var area = Math.Round(areaSqm);
        if (area is < MinArea or > MaxArea)
            return null;

        var ci = CultureInfo.InvariantCulture;
        var lat = location.Latitude.ToString("0.000000", ci);
        var lng = location.Longitude.ToString("0.000000", ci);
        var key = $"{lat}|{lng}|{area.ToString(ci)}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var url = $"https://deweloperuch.pl/api/standalone-valuation?area={area.ToString(ci)}&lat={lat}&lng={lng}";
            using var response = await _httpClientFactory.CreateClient(HttpClientName)
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    // No transactions near this point.
                    _cache[key] = null;
                    return null;
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests:
                    _blockedUntil = _timeProvider.GetUtcNow() + _options.RcnValuationBlockedCooldown;
                    _logger.LogWarning(
                        "Deweloperuch valuation refused access ({Status}); disabled until {Until:u}",
                        (int)response.StatusCode, _blockedUntil);
                    return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Deweloperuch valuation returned {Status}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var valuation = await ParseResponseAsync(stream, cancellationToken).ConfigureAwait(false);
            _cache[key] = valuation;
            return valuation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deweloperuch valuation failed");
            return null;
        }
    }

    private bool IsBlocked() => _timeProvider.GetUtcNow() < _blockedUntil;

    /// <summary>
    /// Maps only the summary fields; the response also carries large transaction lists we ignore.
    /// </summary>
    internal static async Task<RcnValuation?> ParseResponseAsync(Stream json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryGetDecimal(root, "estimatedPricePerSqm", out var estimated) || estimated <= 0)
            {
                return null;
            }

            decimal? median = null;
            var count = 0;
            if (root.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                if (TryGetDecimal(stats, "medianPricePerSqm", out var m) && m > 0)
                    median = m;
                if (TryGetDecimal(stats, "count", out var c))
                    count = (int)c;
            }

            var radius = TryGetDecimal(root, "radiusKm", out var r) ? (double)r : 0;
            var period = TryGetDecimal(root, "periodMonths", out var p) ? (int)p : 0;
            return new RcnValuation(Math.Round(estimated), median is null ? null : Math.Round(median.Value), count, radius, period);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetDecimal(JsonElement element, string name, out decimal value)
    {
        value = 0;
        return element.TryGetProperty(name, out var prop) &&
               prop.ValueKind == JsonValueKind.Number &&
               prop.TryGetDecimal(out value);
    }
}
