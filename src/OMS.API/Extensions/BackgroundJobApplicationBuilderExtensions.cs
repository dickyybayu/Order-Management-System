using Coravel;
using OMS.API.Infrastructure.Jobs;
using OMS.API.Infrastructure.Shareds.Models;

namespace OMS.API.Extensions;

public static class BackgroundJobApplicationBuilderExtensions
{
    public static WebApplication UseOmsBackgroundJobs(this WebApplication app)
    {
        app.Services.UseScheduler(scheduler =>
        {
            scheduler
                .Schedule<DailySalesReportJob>()
                .DailyAt(0, 0)
                .PreventOverlapping(nameof(DailySalesReportJob));
        })
        .OnError(exception =>
        {
            var logger = app.Services.GetRequiredService<ILogger<DailySalesReportJob>>();
            logger.LogWarning(exception, "Daily sales report scheduler failed.");
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapPost(
                "/api/v1/dev/jobs/daily-sales-report/run",
                async (
                    IManualDailySalesReportJobRunner runner,
                    CancellationToken cancellationToken) =>
                {
                    await runner.RunAsync(cancellationToken);

                    return Results.Ok(ApiResponse.Ok("Daily sales report job invoked successfully."));
                })
                .ExcludeFromDescription();
        }

        return app;
    }
}
