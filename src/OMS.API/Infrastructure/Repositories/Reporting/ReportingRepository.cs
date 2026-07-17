using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Databases;
using BackgroundJobExecutionEntity = global::OMS.API.Models.BackgroundJobExecution;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;
using OrderEntity = global::OMS.API.Models.Order;
using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Reporting;

public sealed class ReportingRepository(ApplicationDbContext dbContext) : IReportingRepository
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<DailySalesReportEntity?> GetDailySalesReportByDateAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken)
    {
        return GetPersistedDailySalesReportByDateAsync(reportDate, cancellationToken);
    }

    public Task<DailySalesReportEntity?> GetPersistedDailySalesReportByDateAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken)
    {
        return dbContext.DailySalesReports
            .AsNoTracking()
            .Include(report => report.Items)
            .SingleOrDefaultAsync(report => report.ReportDate == reportDate, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderEntity>> ListDeliveredOrdersForDateAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order =>
                order.Status == OrderStatusEntity.Delivered &&
                order.CreatedAtUtc >= startUtc &&
                order.CreatedAtUtc < endUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDailySalesReportAsync(DailySalesReportEntity report, CancellationToken cancellationToken)
    {
        await dbContext.DailySalesReports.AddAsync(report, cancellationToken);
    }

    public async Task AddBackgroundJobExecutionAsync(
        BackgroundJobExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        await dbContext.BackgroundJobExecutions.AddAsync(execution, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public void ClearChanges()
    {
        dbContext.ChangeTracker.Clear();
    }
}
