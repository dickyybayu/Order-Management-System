using System.ComponentModel.DataAnnotations;
using OMS.API.Infrastructure.Shareds.Pagination;

namespace OMS.API.Domain.Supplier.Dtos;

public sealed record SupplierListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = PaginationRequest.DefaultPage;

    [Range(1, PaginationRequest.MaxPageSize)]
    public int PageSize { get; init; } = PaginationRequest.DefaultPageSize;

    public string? Search { get; init; }

    public string? SortBy { get; init; } = "createdAt";

    public SortDirection SortDirection { get; init; } = SortDirection.Desc;

    public int Skip => (Page - 1) * PageSize;
}
