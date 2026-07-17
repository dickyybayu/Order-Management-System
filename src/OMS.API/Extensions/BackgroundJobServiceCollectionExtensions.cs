using Coravel;
using OMS.API.Infrastructure.Jobs;

namespace OMS.API.Extensions;

public static class BackgroundJobServiceCollectionExtensions
{
    public static IServiceCollection AddOmsBackgroundJobs(this IServiceCollection services)
    {
        services.AddScheduler();
        services.AddQueue();
        services.AddTransient<DailySalesReportJob>();
        services.AddTransient<IManualDailySalesReportJobRunner, ManualDailySalesReportJobRunner>();

        return services;
    }
}
