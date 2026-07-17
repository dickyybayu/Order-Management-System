namespace OMS.API.Tests.Unit;

public sealed class OrderServiceShippingTests : TestBase
{
    [Fact]
    public async Task OrderShipEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/ship",
            CreateJsonContent(new ShipOrderRequest("JNE-123456")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SalesOperatorCannotShipOrder()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/ship",
            CreateJsonContent(new ShipOrderRequest("JNE-123456")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanShipOrder(string roleName)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/ship",
            CreateJsonContent(new ShipOrderRequest(" JNE-123456 ")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(nameof(OrderStatus.Shipped), body, StringComparison.Ordinal);
        Assert.Contains("JNE-123456", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShipOrderRejectsMissingOrBlankTrackingNumber(string trackingNumber)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/ship",
            CreateJsonContent(new ShipOrderRequest(trackingNumber)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(ShipOrderRequest.TrackingNumber), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ShipOrderRejectsTooLongTrackingNumber()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/ship",
            CreateJsonContent(new ShipOrderRequest(new string('A', 101))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task ShipMissingOrderReturnsNotFound()
    {
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(new FakeOrderRepository(), notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ShipAsync(Guid.NewGuid(), new ShipOrderRequest("JNE-123456"), CancellationToken.None));

        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OnlyProcessingOrdersCanBeShipped(OrderStatus status)
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", status: status);
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.ShipAsync(order.Id, new ShipOrderRequest("JNE-123456"), CancellationToken.None));

        Assert.Equal(status, order.Status);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task ShippingProcessingOrderUpdatesStatusTrackingHistoryAndLeavesStockUnchanged()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Processing,
            trackingNumber: null);
        var stockBeforeShipment = repository.Products.Single().Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, SystemRoleNames.Supervisor, repository.CurrentUser.Id, notificationQueue);

        var response = await service.ShipAsync(
            order.Id,
            new ShipOrderRequest(" JNE-123456 "),
            CancellationToken.None);

        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Shipped, response.Status);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Equal("JNE-123456", response.TrackingNumber);
        Assert.NotNull(order.UpdatedAtUtc);
        Assert.Equal(OrderStatus.Processing, history.FromStatus);
        Assert.Equal(OrderStatus.Shipped, history.ToStatus);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedByUserId);
        Assert.NotEqual(default, history.ChangedAtUtc);
        Assert.Equal(stockBeforeShipment, repository.Products.Single().Stock);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
        Assert.Equal(OrderStatus.Processing, notificationQueue.LastFromStatus);
        Assert.Equal(OrderStatus.Shipped, notificationQueue.LastToStatus);
    }


    [Fact]
    public async Task ShipmentWriteFailureRollsBackStatusTrackingHistoryAndNotification()
    {
        var repository = new FakeOrderRepository { ThrowOnSave = true };
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Processing,
            trackingNumber: null);
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ShipAsync(order.Id, new ShipOrderRequest("JNE-123456"), CancellationToken.None));

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Null(order.TrackingNumber);
        Assert.Null(order.UpdatedAtUtc);
        Assert.Empty(order.StatusHistory);
        Assert.False(repository.TransactionCommitted);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task ShipmentConcurrencyConflictReturnsConflictAndDoesNotNotify()
    {
        var repository = new FakeOrderRepository { ThrowConcurrencyOnSave = true };
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Processing,
            trackingNumber: null);
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.ShipAsync(order.Id, new ShipOrderRequest("JNE-123456"), CancellationToken.None));

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Null(order.TrackingNumber);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task NotificationFailureDoesNotUndoShipment()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Processing,
            trackingNumber: null);
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted)
        {
            ThrowOnEnqueue = true
        };
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        var response = await service.ShipAsync(order.Id, new ShipOrderRequest("JNE-123456"), CancellationToken.None);

        Assert.Equal(OrderStatus.Shipped, response.Status);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Single(order.StatusHistory);
        Assert.True(repository.TransactionCommitted);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
    }


    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OnlyShippedOrdersCanBeDelivered(OrderStatus status)
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", status: status);
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.DeliverAsync(order.Id, CancellationToken.None));

        Assert.Equal(status, order.Status);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task DeliveringShippedOrderUpdatesStatusHistoryAndLeavesTrackingAndStockUnchanged()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: OrderStatus.Shipped,
            trackingNumber: "JNE-123456");
        var stockBeforeDelivery = repository.Products.Single().Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, SystemRoleNames.Supervisor, repository.CurrentUser.Id, notificationQueue);

        var response = await service.DeliverAsync(order.Id, CancellationToken.None);

        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Delivered, response.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal("JNE-123456", order.TrackingNumber);
        Assert.Equal("JNE-123456", response.TrackingNumber);
        Assert.NotNull(order.UpdatedAtUtc);
        Assert.Equal(OrderStatus.Shipped, history.FromStatus);
        Assert.Equal(OrderStatus.Delivered, history.ToStatus);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedByUserId);
        Assert.NotEqual(default, history.ChangedAtUtc);
        Assert.Equal(stockBeforeDelivery, repository.Products.Single().Stock);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
        Assert.Equal(OrderStatus.Shipped, notificationQueue.LastFromStatus);
        Assert.Equal(OrderStatus.Delivered, notificationQueue.LastToStatus);
    }


    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task ShippedDeliveredAndCancelledOrdersCannotBeCancelled(OrderStatus status)
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", status: status);
        var product = repository.Products.Single();
        var stockBeforeCancellation = product.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelAsync(
                order.Id,
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(status, order.Status);
        Assert.Equal(stockBeforeCancellation, product.Stock);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }

}

