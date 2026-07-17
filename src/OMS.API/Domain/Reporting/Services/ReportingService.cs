using OMS.API.Domain.Reporting.Dtos;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Infrastructure.Exceptions;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;

namespace OMS.API.Domain.Reporting.Services;

public sealed class ReportingService(IReportingRepository reportingRepository) : IReportingService
{
    public async Task<DailySalesReportResponse> GetDailySalesReportAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken)
    {
        var report = await reportingRepository.GetPersistedDailySalesReportByDateAsync(
            reportDate,
            cancellationToken)
            ?? throw new NotFoundException("Daily sales report was not found.");

        return MapReport(report);
    }

    private static DailySalesReportResponse MapReport(DailySalesReportEntity report)
    {
        return new DailySalesReportResponse(
            report.Id,
            report.ReportDate,
            report.TotalOrders,
            report.TotalRevenue,
            report.GeneratedAtUtc,
            report.Items
                .OrderBy(item => item.ProductSku)
                .ThenBy(item => item.ProductName)
                .ThenBy(item => item.ProductId)
                .Select(item => new DailySalesReportItemResponse(
                    item.Id,
                    item.ProductId,
                    item.ProductSku,
                    item.ProductName,
                    item.QuantitySold,
                    item.Revenue))
                .ToArray());
    }
}
