namespace OMS.API.Tests.Unit;

public sealed class DailySalesReportingTests : TestBase
{
    [Fact]
    public void DailySalesReportingEntitiesAreMappedAccordingToTask26()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var reportEntity = dbContext.Model.FindEntityType(typeof(DailySalesReport));
        var itemEntity = dbContext.Model.FindEntityType(typeof(DailySalesReportItem));
        var executionEntity = dbContext.Model.FindEntityType(typeof(BackgroundJobExecution));

        Assert.NotNull(reportEntity);
        Assert.NotNull(itemEntity);
        Assert.NotNull(executionEntity);
        Assert.Equal("DailySalesReports", reportEntity.GetTableName());
        Assert.Equal("DailySalesReportItems", itemEntity.GetTableName());
        Assert.Equal("BackgroundJobExecutions", executionEntity.GetTableName());
        Assert.False(reportEntity.FindProperty(nameof(DailySalesReport.ReportDate))?.IsNullable);
        Assert.False(reportEntity.FindProperty(nameof(DailySalesReport.TotalOrders))?.IsNullable);
        Assert.False(reportEntity.FindProperty(nameof(DailySalesReport.TotalRevenue))?.IsNullable);
        Assert.Equal(18, reportEntity.FindProperty(nameof(DailySalesReport.TotalRevenue))?.GetPrecision());
        Assert.Equal(2, reportEntity.FindProperty(nameof(DailySalesReport.TotalRevenue))?.GetScale());
        Assert.Equal(18, itemEntity.FindProperty(nameof(DailySalesReportItem.Revenue))?.GetPrecision());
        Assert.Equal(2, itemEntity.FindProperty(nameof(DailySalesReportItem.Revenue))?.GetScale());
        Assert.Equal(50, itemEntity.FindProperty(nameof(DailySalesReportItem.ProductSku))?.GetMaxLength());
        Assert.Equal(150, itemEntity.FindProperty(nameof(DailySalesReportItem.ProductName))?.GetMaxLength());
        Assert.Equal(150, executionEntity.FindProperty(nameof(BackgroundJobExecution.JobName))?.GetMaxLength());
        Assert.Equal(30, executionEntity.FindProperty(nameof(BackgroundJobExecution.Status))?.GetMaxLength());
        Assert.Equal("nvarchar(max)", executionEntity.FindProperty(nameof(BackgroundJobExecution.Message))?.GetColumnType());
    }


    [Fact]
    public void DailySalesReportingIndexesAndDeleteBehaviorAreConfigured()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var reportEntity = dbContext.Model.FindEntityType(typeof(DailySalesReport));
        var itemEntity = dbContext.Model.FindEntityType(typeof(DailySalesReportItem));
        var executionEntity = dbContext.Model.FindEntityType(typeof(BackgroundJobExecution));

        Assert.NotNull(reportEntity);
        Assert.NotNull(itemEntity);
        Assert.NotNull(executionEntity);
        Assert.Contains(
            reportEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(DailySalesReport.ReportDate));
        Assert.Contains(
            executionEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(BackgroundJobExecution.JobName));
        Assert.Contains(
            executionEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(BackgroundJobExecution.StartedAtUtc));
        Assert.Contains(
            executionEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(BackgroundJobExecution.Status));

        var reportItemReportFk = itemEntity.GetForeignKeys()
            .Single(fk => fk.Properties.Single().Name == nameof(DailySalesReportItem.DailySalesReportId));
        var reportItemProductFk = itemEntity.GetForeignKeys()
            .Single(fk => fk.Properties.Single().Name == nameof(DailySalesReportItem.ProductId));

        Assert.Equal(DeleteBehavior.Cascade, reportItemReportFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, reportItemProductFk.DeleteBehavior);
    }


    [Fact]
    public async Task EmptyDayCreatesReportWithZeroTotalsAndNoItems()
    {
        var repository = new FakeReportingRepository();
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(new DateOnly(2026, 7, 17), CancellationToken.None);

        Assert.Equal(0, response.TotalOrders);
        Assert.Equal(0m, response.TotalRevenue);
        Assert.Empty(response.Items);
        Assert.Single(repository.Reports);
        var execution = Assert.Single(repository.Executions);
        Assert.Equal(BackgroundJobExecutionStatus.Succeeded, execution.Status);
        Assert.NotNull(execution.FinishedAtUtc);
    }


    [Fact]
    public async Task ReportDateWindowUsesInclusiveStartAndExclusiveEnd()
    {
        var repository = new FakeReportingRepository();
        var reportDate = new DateOnly(2026, 7, 17);
        var start = reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var startBoundaryOrder = repository.AddOrder(OrderStatus.Delivered, start, totalAmount: 10m);
        repository.AddItem(startBoundaryOrder, productSku: "START", productName: "Start Product", quantity: 1, lineTotal: 10m);
        var endBoundaryOrder = repository.AddOrder(OrderStatus.Delivered, end, totalAmount: 20m);
        repository.AddItem(endBoundaryOrder, productSku: "END", productName: "End Product", quantity: 1, lineTotal: 20m);
        var beforeOrder = repository.AddOrder(OrderStatus.Delivered, start.AddTicks(-1), totalAmount: 30m);
        repository.AddItem(beforeOrder, productSku: "BEFORE", productName: "Before Product", quantity: 1, lineTotal: 30m);
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(reportDate, CancellationToken.None);

        Assert.Equal(1, response.TotalOrders);
        Assert.Equal(10m, response.TotalRevenue);
        Assert.Equal("START", Assert.Single(response.Items).ProductSku);
        Assert.Equal(start, repository.LastStartUtc);
        Assert.Equal(end, repository.LastEndUtc);
    }


    [Fact]
    public async Task ReportAggregatesProductQuantitiesRevenueAndSnapshots()
    {
        var repository = new FakeReportingRepository();
        var reportDate = new DateOnly(2026, 7, 17);
        var start = reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var productId = Guid.NewGuid();
        var firstOrder = repository.AddOrder(OrderStatus.Delivered, start.AddHours(1), totalAmount: 25m);
        repository.AddItem(firstOrder, productId, "SKU-1", "Snapshot Name", quantity: 2, lineTotal: 20m);
        repository.AddItem(firstOrder, productId, "SKU-1", "Snapshot Name", quantity: 1, lineTotal: 5m);
        var secondOrder = repository.AddOrder(OrderStatus.Delivered, start.AddHours(2), totalAmount: 15m);
        repository.AddItem(secondOrder, productId, "SKU-1", "Snapshot Name", quantity: 3, lineTotal: 15m);
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(reportDate, CancellationToken.None);

        Assert.Equal(2, response.TotalOrders);
        Assert.Equal(40m, response.TotalRevenue);
        var item = Assert.Single(response.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("SKU-1", item.ProductSku);
        Assert.Equal("Snapshot Name", item.ProductName);
        Assert.Equal(6, item.QuantitySold);
        Assert.Equal(40m, item.Revenue);
    }


    [Fact]
    public async Task SameDateGenerationReturnsExistingReportWithoutDuplicate()
    {
        var repository = new FakeReportingRepository();
        var reportDate = new DateOnly(2026, 7, 17);
        var existingReport = repository.AddExistingReport(reportDate, totalOrders: 4, totalRevenue: 99m);
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(reportDate, CancellationToken.None);

        Assert.Equal(existingReport.Id, response.Id);
        Assert.Equal(4, response.TotalOrders);
        Assert.Single(repository.Reports);
        var execution = Assert.Single(repository.Executions);
        Assert.Equal(BackgroundJobExecutionStatus.Succeeded, execution.Status);
        Assert.Equal("Existing report returned.", execution.Message);
    }


    [Fact]
    public async Task DuplicateReportDateRaceReturnsExistingReportSafely()
    {
        var reportDate = new DateOnly(2026, 7, 17);
        var repository = new FakeReportingRepository { ThrowDuplicateOnReportAdd = true };
        var existingReport = repository.AddExistingReport(reportDate, totalOrders: 2, totalRevenue: 25m);
        repository.HideExistingReportUntilDuplicate = true;
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(reportDate, CancellationToken.None);

        Assert.Equal(existingReport.Id, response.Id);
        Assert.Single(repository.Reports);
        Assert.Contains(repository.Executions, execution => execution.Status == BackgroundJobExecutionStatus.Failed);
    }


    [Fact]
    public async Task FailedGenerationPersistsFailedExecutionAndLeavesNoPartialReport()
    {
        var repository = new FakeReportingRepository { ThrowOnListOrders = true };
        var generator = CreateDailySalesReportGenerator(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(new DateOnly(2026, 7, 17), CancellationToken.None));

        Assert.Empty(repository.Reports);
        var execution = Assert.Single(repository.Executions);
        Assert.Equal(BackgroundJobExecutionStatus.Failed, execution.Status);
        Assert.NotNull(execution.FinishedAtUtc);
        Assert.Equal("Report generation failed.", execution.Message);
        Assert.DoesNotContain(" at ", execution.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task FailureAfterReportAddRollsBackPartialReportBeforeFailedExecutionIsSaved()
    {
        var repository = new FakeReportingRepository { ThrowOnSaveReports = true };
        var reportDate = new DateOnly(2026, 7, 17);
        var order = repository.AddOrder(
            OrderStatus.Delivered,
            reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(1),
            totalAmount: 10m);
        repository.AddItem(order, productSku: "SKU-1", productName: "Product 1", quantity: 1, lineTotal: 10m);
        var generator = CreateDailySalesReportGenerator(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(reportDate, CancellationToken.None));

        Assert.Empty(repository.Reports);
        var execution = Assert.Single(repository.Executions);
        Assert.Equal(BackgroundJobExecutionStatus.Failed, execution.Status);
    }


    [Fact]
    public void CoravelServicesAndInvocablesAreRegistered()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IDailySalesReportGenerator, FakeDailySalesReportGenerator>();
        services.AddOmsBackgroundJobs();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<IQueue>());
        Assert.NotNull(serviceProvider.GetService<DailySalesReportJob>());
        Assert.NotNull(serviceProvider.GetService<IManualDailySalesReportJobRunner>());
    }


    [Fact]
    public void SchedulerRegistrationConfiguresDailySalesReportForMidnightUtc()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Extensions", "BackgroundJobApplicationBuilderExtensions.cs"));

        Assert.Contains("UseScheduler", source, StringComparison.Ordinal);
        Assert.Contains(".Schedule<DailySalesReportJob>()", source, StringComparison.Ordinal);
        Assert.Contains(".DailyAt(0, 0)", source, StringComparison.Ordinal);
        Assert.Contains("PreventOverlapping", source, StringComparison.Ordinal);
    }


    [Fact]
    public void ManualDailySalesReportEndpointIsDevelopmentOnly()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Extensions", "BackgroundJobApplicationBuilderExtensions.cs"));

        Assert.Contains("app.Environment.IsDevelopment()", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/dev/jobs/daily-sales-report/run", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet(\"/api/v1/reports", source, StringComparison.Ordinal);
    }


    [Fact]
    public async Task DailySalesReportInvocableCallsGeneratorForPreviousUtcDateAndForwardsCancellationToken()
    {
        var generator = new FakeDailySalesReportGenerator();
        var job = new DailySalesReportJob(generator, NullLogger<DailySalesReportJob>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();

        await job.InvokeAsync(cancellationTokenSource.Token);

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), generator.LastReportDate);
        Assert.Equal(cancellationTokenSource.Token, generator.LastCancellationToken);
        Assert.Equal(1, generator.CallCount);
    }


    [Fact]
    public async Task DailySalesReportInvocableLogsAndRethrowsFailures()
    {
        var generator = new FakeDailySalesReportGenerator { ThrowOnGenerate = true };
        var logger = new TestLogger<DailySalesReportJob>();
        var job = new DailySalesReportJob(generator, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.InvokeAsync(CancellationToken.None));

        Assert.Contains(logger.Entries, entry =>
            entry.LogLevel == LogLevel.Warning &&
            entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase));
    }

}

