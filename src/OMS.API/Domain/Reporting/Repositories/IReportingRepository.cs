using BackgroundJobExecutionEntity = global::OMS.API.Models.BackgroundJobExecution;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;
using OrderEntity = global::OMS.API.Models.Order;
namespace OMS.API.Domain.Reporting.Repositories;

public interface IReportingRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task<DailySalesReportEntity?> GetDailySalesReportByDateAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken);

    Task<DailySalesReportEntity?> GetPersistedDailySalesReportByDateAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrderEntity>> ListDeliveredOrdersForDateAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken);

    Task AddDailySalesReportAsync(DailySalesReportEntity report, CancellationToken cancellationToken);

    Task AddBackgroundJobExecutionAsync(BackgroundJobExecutionEntity execution, CancellationToken cancellationToken);

    void ClearChanges();

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
