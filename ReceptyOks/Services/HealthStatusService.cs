using ReceptyOks.Interfaces;
using ReceptyOks.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReceptyOks.Services;

/// <summary>
/// Service for checking backend health status via health endpoints
/// </summary>
public class HealthStatusService : IHealthStatusService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HealthStatusService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new TimeSpanConverter() }
    };

    public HealthStatusService(HttpClient httpClient, ILogger<HealthStatusService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthStatusResponse> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = new HealthStatusResponse { CheckedAt = DateTime.UtcNow };

        try
        {
            // Check network connectivity first
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                response.Status = "Unhealthy";
                response.ErrorMessage = "Brak połączenia z internetem";
                return response;
            }

            using var httpResponse = await _httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);

            if (httpResponse.IsSuccessStatusCode)
            {
                var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // ASP.NET Core health checks can return plain text ("Healthy") or JSON
                if (content.StartsWith('{'))
                {
                    // JSON response with detailed health report
                    var healthReport = JsonSerializer.Deserialize<HealthReportDto>(content, JsonOptions);

                    if (healthReport != null)
                    {
                        response.Status = healthReport.Status;
                        response.TotalDuration = healthReport.TotalDuration;
                        response.Entries = healthReport.Entries.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new HealthCheckEntry
                            {
                                Status = kvp.Value.Status,
                                Duration = kvp.Value.Duration,
                                Description = kvp.Value.Description,
                                Exception = kvp.Value.Exception,
                                Data = kvp.Value.Data,
                                Tags = kvp.Value.Tags ?? []
                            });
                        response.IsSuccess = true;
                    }
                }
                else
                {
                    // Plain text response (e.g., "Healthy", "Degraded", "Unhealthy")
                    response.Status = content.Trim();
                    response.IsSuccess = true;
                }
            }
            else
            {
                response.Status = "Unhealthy";
                response.ErrorMessage = $"Serwer zwrócił błąd: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}";
                _logger.LogWarning("Health check failed with status {StatusCode}", httpResponse.StatusCode);
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            response.Status = "Unknown";
            response.ErrorMessage = "Sprawdzanie anulowane";
            throw;
        }
        catch (HttpRequestException ex)
        {
            response.Status = "Unhealthy";
            response.ErrorMessage = $"Nie można połączyć się z serwerem: {ex.Message}";
            _logger.LogError(ex, "Failed to connect to health endpoint");
        }
        catch (Exception ex)
        {
            response.Status = "Unhealthy";
            response.ErrorMessage = $"Błąd: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during health check");
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<bool> IsAliveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return false;
            }

            using var response = await _httpClient.GetAsync("/alive", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Liveness check failed");
            return false;
        }
    }

    /// <summary>
    /// DTO matching ASP.NET Core HealthReport JSON structure
    /// </summary>
    private sealed class HealthReportDto
    {
        public string Status { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }
        public Dictionary<string, HealthCheckEntryDto> Entries { get; set; } = [];
    }

    private sealed class HealthCheckEntryDto
    {
        public string Status { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? Description { get; set; }
        public string? Exception { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public IEnumerable<string>? Tags { get; set; }
    }

    /// <summary>
    /// Custom converter for TimeSpan as ASP.NET Core serializes it as string
    /// </summary>
    private sealed class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (TimeSpan.TryParse(value, out var timeSpan))
                {
                    return timeSpan;
                }
            }
            return TimeSpan.Zero;
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
