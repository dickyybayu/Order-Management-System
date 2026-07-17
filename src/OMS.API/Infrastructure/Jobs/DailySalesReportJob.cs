using Coravel.Invocable;
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

namespace OMS.API.Infrastructure.Jobs;

public sealed class DailySalesReportJob(
    IDailySalesReportGenerator reportGenerator,
    ILogger<DailySalesReportJob> logger) : IInvocable
{
    public Task Invoke()
    {
        return InvokeAsync(CancellationToken.None);
    }

    public async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        logger.LogInformation(
            "Daily sales report job started for report date {ReportDate}",
            reportDate);

        try
        {
            await reportGenerator.GenerateAsync(reportDate, cancellationToken);

            logger.LogInformation(
                "Daily sales report job finished for report date {ReportDate}",
                reportDate);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Daily sales report job failed for report date {ReportDate}",
                reportDate);
            throw;
        }
    }
}
