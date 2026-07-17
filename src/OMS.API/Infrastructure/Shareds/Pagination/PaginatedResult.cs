namespace OMS.API.Infrastructure.Shareds.Pagination;

public sealed record PaginatedResult<T>(
    IReadOnlyCollection<T> Items,
    PaginationMetadata Pagination)
{
    public PaginatedResult(IEnumerable<T> items, PaginationMetadata pagination)
        : this(items.ToArray(), pagination)
    {
    }
}
