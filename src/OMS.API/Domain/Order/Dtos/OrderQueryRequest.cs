using System.ComponentModel.DataAnnotations;
using OMS.API.Infrastructure.Shareds.Pagination;

namespace OMS.API.Domain.Order.Dtos;

public sealed record OrderQueryRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = PaginationRequest.DefaultPage;

    [Range(1, PaginationRequest.MaxPageSize)]
    public int PageSize { get; init; } = PaginationRequest.DefaultPageSize;

    public string? Status { get; init; }

    public Guid? CustomerId { get; init; }

    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public string? SortBy { get; init; } = "createdAt";

    public SortDirection SortDirection { get; init; } = SortDirection.Desc;

    public int Skip => (Page - 1) * PageSize;
}
