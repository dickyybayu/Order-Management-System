using Microsoft.EntityFrameworkCore;
using OMS.API.Domain.Reporting.Dtos;
using BackgroundJobExecutionEntity = global::OMS.API.Models.BackgroundJobExecution;
using BackgroundJobExecutionStatusEntity = global::OMS.API.Models.BackgroundJobExecutionStatus;
using DailySalesReportEntity = global::OMS.API.Models.DailySalesReport;
using DailySalesReportItemEntity = global::OMS.API.Models.DailySalesReportItem;
using OrderEntity = global::OMS.API.Models.Order;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;
using OMS.API.Domain.Auth.Services;
using OMS.API.Domain.Auth.Token;
using OMS.API.Domain.Category.Services;
using OMS.API.Domain.Customer.Services;
using OMS.API.Domain.ExchangeRate.Services;
using OMS.API.Domain.Order.Services;
using OMS.API.Domain.Product.Services;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Domain.Supplier.Services;
using OMS.API.Domain.User.Services;

namespace OMS.API.Domain.Reporting.Services;

public sealed class DailySalesReportGenerator(
    IReportingRepository reportingRepository,
    ILogger<DailySalesReportGenerator> logger) : IDailySalesReportGenerator
{
    public const string JobName = "DailySalesReportGenerator";

    public async Task<DailySalesReportResponse> GenerateAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var execution = new BackgroundJobExecutionEntity
        {
            JobName = JobName,
            StartedAtUtc = startedAtUtc,
            Status = BackgroundJobExecutionStatusEntity.Running
        };

        try
        {
            var report = await reportingRepository.ExecuteInTransactionAsync(
                operationCancellationToken => GenerateInTransactionAsync(
                    reportDate,
                    execution,
                    operationCancellationToken),
                cancellationToken);

            return MapReport(report);
        }
        catch (DbUpdateException exception) when (IsLikelyReportDateUniqueRace(exception))
        {
            var existingReport = await reportingRepository.GetDailySalesReportByDateAsync(reportDate, cancellationToken);

            if (existingReport is not null)
            {
                await TryPersistFailedExecutionAsync(execution, "Report already existed before this run completed.", cancellationToken);

                return MapReport(existingReport);
            }

            await TryPersistFailedExecutionAsync(execution, "Report generation failed while saving.", cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            await TryPersistFailedExecutionAsync(execution, "Report generation failed.", cancellationToken);
            logger.LogWarning(
                exception,
                "Daily sales report generation failed for {ReportDate}",
                reportDate);
            throw;
        }
    }

    private async Task<DailySalesReportEntity> GenerateInTransactionAsync(
        DateOnly reportDate,
        BackgroundJobExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        var existingReport = await reportingRepository.GetDailySalesReportByDateAsync(reportDate, cancellationToken);

        if (existingReport is not null)
        {
            execution.Status = BackgroundJobExecutionStatusEntity.Succeeded;
            execution.FinishedAtUtc = DateTime.UtcNow;
            execution.Message = "Existing report returned.";
            await reportingRepository.AddBackgroundJobExecutionAsync(execution, cancellationToken);
            await reportingRepository.SaveChangesAsync(cancellationToken);

            return existingReport;
        }

        var generatedAtUtc = DateTime.UtcNow;
        var startUtc = reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);
        var orders = await reportingRepository.ListDeliveredOrdersForDateAsync(startUtc, endUtc, cancellationToken);
        var report = new DailySalesReportEntity
        {
            ReportDate = reportDate,
            TotalOrders = orders.Count,
            TotalRevenue = orders.Sum(order => order.TotalAmount),
            GeneratedAtUtc = generatedAtUtc
        };

        foreach (var itemGroup in orders
            .SelectMany(order => order.Items)
            .GroupBy(item => new { item.ProductId, item.ProductSku, item.ProductName }))
        {
            report.Items.Add(new DailySalesReportItemEntity
            {
                ProductId = itemGroup.Key.ProductId,
                ProductSku = itemGroup.Key.ProductSku,
                ProductName = itemGroup.Key.ProductName,
                QuantitySold = itemGroup.Sum(item => item.Quantity),
                Revenue = itemGroup.Sum(item => item.LineTotal)
            });
        }

        execution.Status = BackgroundJobExecutionStatusEntity.Succeeded;
        execution.FinishedAtUtc = DateTime.UtcNow;
        execution.Message = "Report generated successfully.";

        await reportingRepository.AddDailySalesReportAsync(report, cancellationToken);
        await reportingRepository.AddBackgroundJobExecutionAsync(execution, cancellationToken);
        await reportingRepository.SaveChangesAsync(cancellationToken);

        return report;
    }

    private async Task TryPersistFailedExecutionAsync(
        BackgroundJobExecutionEntity execution,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            reportingRepository.ClearChanges();
            execution.Status = BackgroundJobExecutionStatusEntity.Failed;
            execution.FinishedAtUtc = DateTime.UtcNow;
            execution.Message = message;

            await reportingRepository.AddBackgroundJobExecutionAsync(execution, cancellationToken);
            await reportingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to persist daily sales report job failure status for execution {ExecutionId}",
                execution.Id);
        }
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

    private static bool IsLikelyReportDateUniqueRace(DbUpdateException exception)
    {
        return exception.InnerException is not null ||
            exception.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
