namespace OMS.API.Infrastructure.Jobs;

public sealed class ManualDailySalesReportJobRunner(DailySalesReportJob job) : IManualDailySalesReportJobRunner
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        return job.InvokeAsync(cancellationToken);
    }
}
