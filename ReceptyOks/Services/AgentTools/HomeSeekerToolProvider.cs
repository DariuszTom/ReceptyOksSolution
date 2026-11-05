using ReceptyOks.Interfaces;
using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Shared.AI;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ILogger = Serilog.ILogger;

namespace ReceptyOks.Services.AgentTools
{
    /// <summary>HomeSeeker (real estate search) tools backed by <see cref="IHomeSeekerService"/>.</summary>
    public sealed class HomeSeekerToolProvider : IAgentToolProvider
    {
        private readonly IHomeSeekerService _homeSeekerService;
        private readonly ILogger _logger;

        public HomeSeekerToolProvider(IHomeSeekerService homeSeekerService, ILogger logger)
        {
            _homeSeekerService = homeSeekerService ?? throw new ArgumentNullException(nameof(homeSeekerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AgentToolGroup Group => AgentToolGroup.HomeSeeker;

        public void Register(AiAgent agent)
        {
            ArgumentNullException.ThrowIfNull(agent);

            agent.AddTool<Task<string>>(GetHomeSeekerProfileAsync,
                "homeseeker_get_profile", "Gets the HomeSeeker search profile (city, district, property types, price/area/rooms limits, extra criteria, notification email, 'our location', last scan time).");

            agent.AddToolAsync<string, string>(GetHomeSeekerListingsAsync,
                "homeseeker_get_listings", $"Searches real estate listings found by HomeSeeker scans (with AI score, summary, pros/cons, price assessment, distance). Parameter: queryJson - JSON object (all optional): PageNumber (default 1), PageSize (default 10, max {MaxListingsPageSize}), MinScore (0-100), Sort ({string.Join(", ", ListingSorts)}), PropertyType (\"House\" or \"Apartment\"), Portal (see homeseeker_get_portals), MaxDistanceKm, PriceDropOnly (bool), EvaluatedOnly (bool). Pass \"{{}}\" for defaults.");

            agent.AddTool<Task<string>>(GetHomeSeekerPortalsAsync,
                "homeseeker_get_portals", "Returns names of real estate portals scanned by HomeSeeker (values for the Portal filter).");

            agent.AddToolAsync<int, string>(GetHomeSeekerScansAsync,
                "homeseeker_get_scans", $"Returns recent HomeSeeker scan runs (status, start/end time, found/new listings, price drops, evaluated count, report availability). Parameter: count - number of scans (1-{MaxScansCount}).");

            agent.AddToolAsync<string, string>(GetHomeSeekerScanReportAsync,
                "homeseeker_get_scan_report", "Returns the plain-text content of a HomeSeeker scan report. Parameter: scanId - the GUID of the scan (from homeseeker_get_scans, only when HasReport is true).");
        }

        private const int MaxListingsPageSize = 25;
        private const int MaxScansCount = 50;
        private const int MaxReportLength = 12000;
        private static readonly string[] ListingSorts = ["score", "price", "price_desc", "pricesqm", "area_desc", "distance", "firstseen"];

        private static readonly JsonSerializerOptions ToolJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private async Task<string> GetHomeSeekerProfileAsync()
        {
            var result = await _homeSeekerService.GetProfileAsync().ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null)
            {
                return Failure(result.Status, result.Message, result.Status == HomeSeekerCallStatus.NotFound
                    ? "HomeSeeker profile has not been configured yet"
                    : null);
            }

            return Serialize(ProjectProfile(result.Value));
        }

        private async Task<string> GetHomeSeekerListingsAsync(string queryJson)
        {
            ListingsToolRequest? request;
            try
            {
                request = string.IsNullOrWhiteSpace(queryJson)
                    ? new ListingsToolRequest()
                    : JsonSerializer.Deserialize<ListingsToolRequest>(queryJson, ToolJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.Warning(ex, "Failed to parse HomeSeeker listings query JSON for AI agent");
                return Serialize(new { error = $"Invalid JSON format: {ex.Message}" });
            }

            request ??= new ListingsToolRequest();

            if (!string.IsNullOrWhiteSpace(request.Sort) && !ListingSorts.Contains(request.Sort, StringComparer.OrdinalIgnoreCase))
            {
                return Serialize(new { error = $"Unsupported sort '{request.Sort}'. Allowed: {string.Join(", ", ListingSorts)}" });
            }

            var query = new HomeSeekerListingQuery
            {
                MinScore = request.MinScore is { } score ? Math.Clamp(score, 0, 100) : null,
                Sort = request.Sort?.ToLowerInvariant(),
                PropertyType = request.PropertyType is HomeSeekerPropertyTypes.House or HomeSeekerPropertyTypes.Apartment
                    ? request.PropertyType
                    : null,
                Portal = string.IsNullOrWhiteSpace(request.Portal) ? null : request.Portal.Trim(),
                MaxDistanceKm = request.MaxDistanceKm is > 0 ? request.MaxDistanceKm : null,
                PriceDropOnly = request.PriceDropOnly,
                EvaluatedOnly = request.EvaluatedOnly
            };

            var pageNumber = Math.Max(1, request.PageNumber ?? 1);
            var pageSize = Math.Clamp(request.PageSize ?? 10, 1, MaxListingsPageSize);

            var result = await _homeSeekerService.GetListingsAsync(pageNumber, pageSize, query).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null)
            {
                return Failure(result.Status, result.Message, errors: result.Errors);
            }

            var page = result.Value;
            return Serialize(new
            {
                page.TotalCount,
                page.PageNumber,
                page.PageSize,
                page.TotalPages,
                Listings = page.Data.Select(l => new
                {
                    l.Id,
                    l.Portal,
                    l.Url,
                    l.Title,
                    l.Price,
                    l.PreviousPrice,
                    l.HasPriceDrop,
                    l.AreaSqm,
                    l.PricePerSqm,
                    l.Location,
                    PropertyType = l.PropertyType.ToString(),
                    l.AiScore,
                    l.AiSummary,
                    l.Pros,
                    l.Cons,
                    l.AiPriceAssessment,
                    l.DistanceFromCenterKm,
                    l.DistanceIsApproximate,
                    FirstSeenAt = l.FirstSeenAtLocal
                })
            });
        }

        private async Task<string> GetHomeSeekerPortalsAsync()
        {
            var result = await _homeSeekerService.GetPortalsAsync().ConfigureAwait(false);
            return result.IsSuccess
                ? Serialize(result.Value ?? [])
                : Failure(result.Status, result.Message);
        }

        private async Task<string> GetHomeSeekerScansAsync(int count)
        {
            var result = await _homeSeekerService.GetScansAsync(Math.Clamp(count <= 0 ? 10 : count, 1, MaxScansCount)).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Failure(result.Status, result.Message);
            }

            return Serialize((result.Value ?? []).Select(s => new
            {
                s.Id,
                Status = s.Status.ToString(),
                StartedAt = s.StartedAtLocal,
                CompletedAt = s.CompletedAt is { } completed ? HomeSeekerTime.ToLocal(completed) : (DateTime?)null,
                s.ListingsFoundCount,
                s.NewListingsCount,
                s.PriceDropsCount,
                s.EvaluatedCount,
                s.HasReport,
                s.Error
            }));
        }

        private async Task<string> GetHomeSeekerScanReportAsync(string scanId)
        {
            if (!Guid.TryParse(scanId, out var id))
            {
                return Serialize(new { error = "Invalid scan ID format" });
            }

            var result = await _homeSeekerService.GetReportHtmlAsync(id).ConfigureAwait(false);
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value))
            {
                return Failure(result.Status, result.Message, result.IsSuccess ? "Report is empty" : null);
            }

            var text = HtmlToText(result.Value);
            var truncated = text.Length > MaxReportLength;
            return Serialize(new
            {
                ScanId = id,
                Truncated = truncated,
                Content = truncated ? text[..MaxReportLength] : text
            });
        }

        private static object ProjectProfile(HomeSeekerProfile p) => new
        {
            p.City,
            p.District,
            PropertyTypes = p.PropertyTypes.ToString(),
            p.MinPrice,
            p.MaxPrice,
            p.MinAreaSqm,
            p.MaxAreaSqm,
            p.MinRooms,
            p.ExtraCriteria,
            p.NotificationEmail,
            p.IsActive,
            p.HomeLatitude,
            p.HomeLongitude,
            p.HomeLabel,
            LastScannedAt = p.LastScannedAtLocal
        };

        private static string HtmlToText(string html)
        {
            var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<(br|/p|/div|/tr|/li|/h\d)[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"[ \t\r\f\v]+", " ");
            text = Regex.Replace(text, @"\s*\n\s*", "\n");
            return text.Trim();
        }

        private static string Failure(HomeSeekerCallStatus status, string? message, string? fallback = null, IReadOnlyDictionary<string, string[]>? errors = null) =>
            Serialize(new
            {
                success = false,
                status = status.ToString(),
                error = fallback ?? message ?? "HomeSeeker request failed",
                validationErrors = errors is { Count: > 0 } ? errors : null
            });

        private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, ToolJsonOptions);

        private sealed class ListingsToolRequest
        {
            public int? PageNumber { get; set; }
            public int? PageSize { get; set; }
            public int? MinScore { get; set; }
            public string? Sort { get; set; }
            public HomeSeekerPropertyTypes? PropertyType { get; set; }
            public string? Portal { get; set; }
            public double? MaxDistanceKm { get; set; }
            public bool PriceDropOnly { get; set; }
            public bool EvaluatedOnly { get; set; }
        }
    }
}
