namespace ReceptyOks.Api.IntegrationTests;

/// <summary>
/// Represents a paginated API response for test deserialization.
/// </summary>
/// <typeparam name="T">The type of items in the Data collection.</typeparam>
public record PaginatedResponse<T>
{
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public List<T> Data { get; init; } = [];
}
