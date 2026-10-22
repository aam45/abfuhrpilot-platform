namespace AbfuhrPilot.Application.Common;

/// <summary>Standard envelope for paginated list endpoints.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Common query parameters for list endpoints.</summary>
public class PageQuery
{
    private const int MaxPageSize = 200;
    private int _pageSize = 25;
    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? 25 : value;
    }
    public string? Search { get; set; }
}
