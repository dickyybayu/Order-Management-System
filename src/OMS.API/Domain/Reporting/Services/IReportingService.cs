using OMS.API.Domain.Reporting.Dtos;

namespace OMS.API.Domain.Reporting.Services;

public interface IReportingService
{
    Task<DailySalesReportResponse> GetDailySalesReportAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken);
}
