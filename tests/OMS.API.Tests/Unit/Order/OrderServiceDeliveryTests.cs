namespace OMS.API.Tests.Unit;

public sealed class OrderServiceDeliveryTests : TestBase
{
    [Fact]
    public async Task OrderDeliverEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/deliver", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SalesOperatorCannotDeliverOrder()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/deliver", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanDeliverOrder(string roleName)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/deliver", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(nameof(OrderStatus.Delivered), body, StringComparison.Ordinal);
        Assert.Contains("JNE-123456", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task DeliverMissingOrderReturnsNotFound()
    {
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(new FakeOrderRepository(), notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeliverAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task DeliveryWriteFailureRollsBackStatusHistoryAndNotification()
    {
        var repository = new FakeOrderRepository { ThrowOnSave = true };
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Shipped,
            trackingNumber: "JNE-123456");
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeliverAsync(order.Id, CancellationToken.None));

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Null(order.UpdatedAtUtc);
        Assert.Empty(order.StatusHistory);
        Assert.False(repository.TransactionCommitted);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task DeliveryConcurrencyConflictReturnsConflictAndDoesNotNotify()
    {
        var repository = new FakeOrderRepository { ThrowConcurrencyOnSave = true };
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Shipped,
            trackingNumber: "JNE-123456");
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.DeliverAsync(order.Id, CancellationToken.None));

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task NotificationFailureDoesNotUndoDelivery()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Shipped,
            trackingNumber: "JNE-123456");
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted)
        {
            ThrowOnEnqueue = true
        };
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        var response = await service.DeliverAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Delivered, response.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Single(order.StatusHistory);
        Assert.True(repository.TransactionCommitted);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
    }


    [Fact]
    public async Task DeliveredOrdersInUtcDateAreCountedAndNonDeliveredAreExcluded()
    {
        var repository = new FakeReportingRepository();
        var reportDate = new DateOnly(2026, 7, 17);
        var start = reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var delivered = repository.AddOrder(OrderStatus.Delivered, start.AddHours(10), totalAmount: 30m);
        repository.AddItem(delivered, productSku: "SKU-1", productName: "Product 1", quantity: 3, lineTotal: 30m);
        var pending = repository.AddOrder(OrderStatus.Pending, start.AddHours(11), totalAmount: 50m);
        repository.AddItem(pending, productSku: "SKU-2", productName: "Product 2", quantity: 5, lineTotal: 50m);
        var generator = CreateDailySalesReportGenerator(repository);

        var response = await generator.GenerateAsync(reportDate, CancellationToken.None);

        Assert.Equal(1, response.TotalOrders);
        Assert.Equal(30m, response.TotalRevenue);
        var item = Assert.Single(response.Items);
        Assert.Equal("SKU-1", item.ProductSku);
        Assert.Equal("Product 1", item.ProductName);
        Assert.Equal(3, item.QuantitySold);
        Assert.Equal(30m, item.Revenue);
    }

}

