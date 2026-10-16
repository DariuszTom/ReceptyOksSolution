using System.Text.Json;

namespace ReceptyOks.Models.HomeSeeker;

/// <summary>Mirrors the server's <c>HomeSeeker.Models.PropertyTypes</c> flags (serialized as a number).</summary>
[Flags]
public enum HomeSeekerPropertyTypes
{
    None = 0,
    House = 1,
    Apartment = 2,
    All = House | Apartment
}

/// <summary>Client-side copy of the shared HomeSeeker search profile (<c>GET/PUT /api/homeseeker/profile</c>).</summary>
public sealed class HomeSeekerProfile
{
    public Guid Id { get; set; }
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }
    public HomeSeekerPropertyTypes PropertyTypes { get; set; } = HomeSeekerPropertyTypes.House;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinAreaSqm { get; set; }
    public decimal? MaxAreaSqm { get; set; }
    public int? MinRooms { get; set; }
    public string? ExtraCriteria { get; set; }
    public string NotificationEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastScannedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DateTime? LastScannedAtLocal => LastScannedAt is { } value ? HomeSeekerTime.ToLocal(value) : null;
}

/// <summary>Body of <c>PUT /api/homeseeker/profile</c>.</summary>
public sealed class HomeSeekerProfileRequest
{
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }
    public HomeSeekerPropertyTypes PropertyTypes { get; set; } = HomeSeekerPropertyTypes.House;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinAreaSqm { get; set; }
    public decimal? MaxAreaSqm { get; set; }
    public int? MinRooms { get; set; }
    public string? ExtraCriteria { get; set; }
    public string NotificationEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>Filters and sort for <c>GET /api/homeseeker/listings</c>; null members are not sent.</summary>
public sealed record HomeSeekerListingQuery
{
    public int? MinScore { get; init; }

    /// <summary>score (default), price, price_desc, pricesqm, area_desc, distance, firstseen.</summary>
    public string? Sort { get; init; }

    public HomeSeekerPropertyTypes? PropertyType { get; init; }
    public string? Portal { get; init; }
    public double? MaxDistanceKm { get; init; }
    public bool PriceDropOnly { get; init; }

    /// <summary>Only listings already evaluated by AI.</summary>
    public bool EvaluatedOnly { get; init; }
}

public sealed class HomeSeekerListing
{
    public Guid Id { get; set; }
    public string Portal { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? PreviousPrice { get; set; }
    public decimal AreaSqm { get; set; }
    public string? Location { get; set; }
    public HomeSeekerPropertyTypes PropertyType { get; set; } = HomeSeekerPropertyTypes.House;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int? AiScore { get; set; }
    public string? AiSummary { get; set; }
    public string? AiProsJson { get; set; }
    public string? AiConsJson { get; set; }
    public string? AiPriceAssessment { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public double? DistanceFromCenterKm { get; set; }
    public bool DistanceIsApproximate { get; set; }

    public string? DistanceText => DistanceFromCenterKm is { } km
        ? $"{(DistanceIsApproximate ? "ok. " : string.Empty)}{km.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)} km do centrum"
        : null;

    public DateTime FirstSeenAtLocal => HomeSeekerTime.ToLocal(FirstSeenAt);

    public string PropertyTypeText => PropertyType == HomeSeekerPropertyTypes.Apartment ? "Mieszkanie" : "Dom";

    public decimal? PricePerSqm => AreaSqm > 0 ? Math.Round(Price / AreaSqm, 0) : null;

    public bool HasPriceDrop => PreviousPrice is { } previous && previous > Price;

    public bool IsEvaluated => AiScore.HasValue;

    public string ScoreText => AiScore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "–";

    public IReadOnlyList<string> Pros => ParseList(AiProsJson);

    public IReadOnlyList<string> Cons => ParseList(AiConsJson);

    public string? ProsText => Pros.Count == 0 ? null : "+ " + string.Join("\n+ ", Pros);

    public string? ConsText => Cons.Count == 0 ? null : "− " + string.Join("\n− ", Cons);

    /// <summary>Only http(s) links may be opened in the browser (the URL originates from scraped content).</summary>
    public bool HasHttpUrl =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static IReadOnlyList<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var items = JsonSerializer.Deserialize<List<string?>>(json);
            return items?.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!.Trim()).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>Mirrors the server enum, which is serialized as a number.</summary>
public enum HomeSeekerScanStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2
}

public sealed class HomeSeekerScanRun
{
    public Guid Id { get; set; }
    public HomeSeekerScanStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ListingsFoundCount { get; set; }
    public int NewListingsCount { get; set; }
    public int PriceDropsCount { get; set; }
    public int EvaluatedCount { get; set; }
    public string? Error { get; set; }

    /// <summary>Report availability flag from the history endpoint (the HTML itself is fetched separately).</summary>
    public bool HasReport { get; set; }

    public DateTime StartedAtLocal => HomeSeekerTime.ToLocal(StartedAt);

    public bool IsFailed => Status == HomeSeekerScanStatus.Failed;

    public string StatusText => Status switch
    {
        HomeSeekerScanStatus.Running => "W toku",
        HomeSeekerScanStatus.Completed => "Zakończony",
        HomeSeekerScanStatus.Failed => "Błąd",
        _ => Status.ToString()
    };
}

public sealed class HomeSeekerPagedResult<T>
{
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public List<T> Data { get; set; } = [];
}

internal static class HomeSeekerTime
{
    // The server stores UTC; EF/SQLite round-trips may drop the Kind, so Unspecified is treated as UTC.
    public static DateTime ToLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value,
        DateTimeKind.Utc => value.ToLocalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
    };
}

public enum HomeSeekerCallStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Unauthorized,
    Error
}

public sealed class HomeSeekerResult<T>
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    private HomeSeekerResult(HomeSeekerCallStatus status, T? value, string? message, IReadOnlyDictionary<string, string[]>? errors)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors ?? NoErrors;
    }

    public HomeSeekerCallStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public bool IsSuccess => Status == HomeSeekerCallStatus.Success;

    public static HomeSeekerResult<T> Ok(T? value) => new(HomeSeekerCallStatus.Success, value, null, null);

    public static HomeSeekerResult<T> Fail(HomeSeekerCallStatus status, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(status, default, message, errors);
}
