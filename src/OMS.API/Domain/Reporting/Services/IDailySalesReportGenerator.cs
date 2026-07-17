using OMS.API.Domain.Reporting.Dtos;

namespace OMS.API.Domain.Reporting.Services;

public interface IDailySalesReportGenerator
{
    Task<DailySalesReportResponse> GenerateAsync(DateOnly reportDate, CancellationToken cancellationToken);
}
