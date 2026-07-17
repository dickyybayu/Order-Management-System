namespace OMS.API.Domain.Reporting.Dtos;

public sealed record DailySalesReportResponse(
    Guid Id,
    DateOnly ReportDate,
    int TotalOrders,
    decimal TotalRevenue,
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<DailySalesReportItemResponse> Items);

public sealed record DailySalesReportItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    int QuantitySold,
    decimal Revenue);
