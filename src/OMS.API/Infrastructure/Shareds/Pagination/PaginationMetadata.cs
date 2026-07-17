namespace OMS.API.Infrastructure.Shareds.Pagination;

public sealed record PaginationMetadata
{
    public PaginationMetadata(int page, int pageSize, int totalItems)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, PaginationRequest.MaxPageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(totalItems);

        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        HasPreviousPage = page > 1;
        HasNextPage = page < TotalPages;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalItems { get; }

    public int TotalPages { get; }

    public bool HasPreviousPage { get; }

    public bool HasNextPage { get; }
}
