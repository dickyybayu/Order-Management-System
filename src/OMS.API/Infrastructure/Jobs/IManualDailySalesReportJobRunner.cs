namespace OMS.API.Infrastructure.Jobs;

public interface IManualDailySalesReportJobRunner
{
    Task RunAsync(CancellationToken cancellationToken);
}
