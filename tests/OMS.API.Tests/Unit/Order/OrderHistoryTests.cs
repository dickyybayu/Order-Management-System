namespace OMS.API.Tests.Unit;

public sealed class OrderHistoryTests : TestBase
{
    [Fact]
    public async Task OrderServiceCreatesOrderWithSnapshotsServerPricesStockReductionAndHistory()
    {
        var repository = new FakeOrderRepository();
        var service = CreateOrderService(repository);
        var product = repository.Products.Single();

        var response = await service.CreateAsync(
            new CreateOrderRequest(
                repository.ActiveCustomer.Id,
                "idr",
                [new CreateOrderItemRequest(product.Id, 2)]),
            CancellationToken.None);

        var order = Assert.Single(repository.Orders);
        var item = Assert.Single(order.Items);
        var history = Assert.Single(order.StatusHistory);
        Assert.Equal("ORD-TEST-0001", response.OrderNumber);
        Assert.Equal(OrderStatus.Pending, response.Status);
        Assert.Equal("IDR", response.CurrencyCode);
        Assert.Equal(20m, response.Subtotal);
        Assert.Equal(20m, response.TotalAmount);
        Assert.Equal(3, product.Stock);
        Assert.Equal(product.SKU, item.ProductSku);
        Assert.Equal(product.Name, item.ProductName);
        Assert.Equal(10m, item.UnitPrice);
        Assert.Equal(20m, item.LineTotal);
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Pending, history.ToStatus);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedByUserId);
        Assert.NotEqual(default, history.ChangedAtUtc);
        Assert.Equal(item.UnitPrice, response.Items.Single().UnitPrice);
    }

    [Fact]
    public async Task OrderServiceAppliesRequestedCurrencyAndPreservesBasePriceSnapshots()
    {
        var repository = new FakeOrderRepository();
        var service = CreateOrderService(repository, exchangeRateService: new FakeExchangeRateService(16000m));
        var product = repository.Products.Single();

        var response = await service.CreateAsync(
            new CreateOrderRequest(
                repository.ActiveCustomer.Id,
                "usd",
                [new CreateOrderItemRequest(product.Id, 2)]),
            CancellationToken.None);

        var item = Assert.Single(response.Items);
        var persistedItem = Assert.Single(repository.Orders.Single().Items);
        Assert.Equal("USD", response.CurrencyCode);
        Assert.Equal(16000m, response.ExchangeRate);
        Assert.Equal(20m, response.Subtotal);
        Assert.Equal(320000m, response.TotalAmount);
        Assert.Equal(10m, item.UnitPrice);
        Assert.Equal(20m, item.LineTotal);
        Assert.Equal(10m, persistedItem.UnitPrice);
        Assert.Equal(20m, persistedItem.LineTotal);
    }


    [Fact]
    public async Task ApprovingPendingOrderUpdatesStatusHistoryAndLeavesStockUnchanged()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var stockBeforeApproval = repository.Products.Single().Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, SystemRoleNames.Supervisor, repository.CurrentUser.Id, notificationQueue);

        var response = await service.ApproveAsync(order.Id, CancellationToken.None);

        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Processing, response.Status);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.NotNull(order.UpdatedAtUtc);
        Assert.Equal(OrderStatus.Pending, history.FromStatus);
        Assert.Equal(OrderStatus.Processing, history.ToStatus);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedByUserId);
        Assert.NotEqual(default, history.ChangedAtUtc);
        Assert.Equal(stockBeforeApproval, repository.Products.Single().Stock);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
    }


    [Fact]
    public async Task OrderHistoryEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanViewAnyOrderHistory(string roleName)
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        repository.AddHistory(otherOrder, null, OrderStatus.Pending, repository.OtherUser);
        var service = CreateOrderService(repository, roleName, repository.CurrentUser.Id);

        var response = await service.GetStatusHistoryAsync(otherOrder.Id, CancellationToken.None);

        var history = Assert.Single(response);
        Assert.Equal(OrderStatus.Pending, history.ToStatus);
        Assert.Equal(repository.OtherUser.Id, history.ChangedBy.UserId);
    }


    [Fact]
    public async Task SalesOperatorCanViewOwnedOrderHistory()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        repository.AddHistory(order, null, OrderStatus.Pending, repository.CurrentUser);
        var service = CreateOrderService(
            repository,
            SystemRoleNames.SalesOperator,
            repository.CurrentUser.Id);

        var response = await service.GetStatusHistoryAsync(order.Id, CancellationToken.None);

        var history = Assert.Single(response);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedBy.UserId);
    }


    [Fact]
    public async Task SalesOperatorViewingAnotherUsersHistoryReceivesNotFound()
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        repository.AddHistory(otherOrder, null, OrderStatus.Pending, repository.OtherUser);
        var service = CreateOrderService(
            repository,
            SystemRoleNames.SalesOperator,
            repository.CurrentUser.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetStatusHistoryAsync(otherOrder.Id, CancellationToken.None));
    }


    [Fact]
    public async Task MissingOrderHistoryReturnsNotFound()
    {
        var service = CreateOrderService(new FakeOrderRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetStatusHistoryAsync(Guid.NewGuid(), CancellationToken.None));
    }


    [Fact]
    public async Task ExistingOrderWithoutHistoryReturnsEmptyCollection()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var service = CreateOrderService(repository);

        var response = await service.GetStatusHistoryAsync(order.Id, CancellationToken.None);

        Assert.Empty(response);
    }


    [Fact]
    public async Task OrderHistoryIsChronologicalWithDeterministicTieBreaker()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var sameTimestamp = DateTime.UtcNow;
        var later = sameTimestamp.AddMinutes(1);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        repository.AddHistory(order, OrderStatus.Processing, OrderStatus.Shipped, changedAtUtc: later);
        repository.AddHistory(order, OrderStatus.Pending, OrderStatus.Processing, changedAtUtc: sameTimestamp, id: secondId);
        repository.AddHistory(order, null, OrderStatus.Pending, changedAtUtc: sameTimestamp, id: firstId);
        var service = CreateOrderService(repository);

        var response = await service.GetStatusHistoryAsync(order.Id, CancellationToken.None);

        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped],
            response.Select(history => history.ToStatus).ToArray());
        Assert.Equal([firstId, secondId], response.Take(2).Select(history => history.Id).ToArray());
    }


    [Fact]
    public async Task OrderHistoryMapsLifecycleValuesAndSafeActorSummary()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var now = DateTime.UtcNow;
        repository.AddHistory(order, null, OrderStatus.Pending, changedAtUtc: now.AddMinutes(1));
        repository.AddHistory(order, OrderStatus.Pending, OrderStatus.Processing, changedAtUtc: now.AddMinutes(2));
        repository.AddHistory(order, OrderStatus.Processing, OrderStatus.Shipped, changedAtUtc: now.AddMinutes(3));
        repository.AddHistory(order, OrderStatus.Shipped, OrderStatus.Delivered, changedAtUtc: now.AddMinutes(4));
        repository.AddHistory(
            order,
            OrderStatus.Processing,
            OrderStatus.Cancelled,
            changedAtUtc: now.AddMinutes(5),
            note: "Customer requested cancellation.");
        var service = CreateOrderService(repository);

        var response = await service.GetStatusHistoryAsync(order.Id, CancellationToken.None);
        var cancelled = response.Last();

        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled],
            response.Select(history => history.ToStatus).ToArray());
        Assert.Null(response.First().FromStatus);
        Assert.Equal(OrderStatus.Processing, cancelled.FromStatus);
        Assert.Equal("Customer requested cancellation.", cancelled.Note);
        Assert.Equal(repository.CurrentUser.Id, cancelled.ChangedBy.UserId);
        Assert.Equal("Admin User", cancelled.ChangedBy.FullName);
        Assert.Equal("admin@example.com", cancelled.ChangedBy.Email);
        Assert.Equal(SystemRoleNames.Admin, cancelled.ChangedBy.Role);
        Assert.DoesNotContain(
            typeof(OrderStatusHistoryResponse).GetProperties(),
            property => property.Name.Equals(nameof(User.PasswordHash), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(OrderHistoryActorResponse).GetProperties(),
            property => property.Name.Equals(nameof(User.PasswordHash), StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public async Task OrderHistoryEndpointDoesNotReturnPasswordHash()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}/history");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void OrderHistoryQueryUsesNoTrackingAndEagerLoading()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Infrastructure", "Repositories", "Order", "OrderRepository.cs"));

        Assert.Contains(".AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains(".Include(order => order.StatusHistory)", source, StringComparison.Ordinal);
        Assert.Contains(".ThenInclude(history => history.ChangedByUser)", source, StringComparison.Ordinal);
        Assert.Contains(".ThenInclude(user => user!.Role)", source, StringComparison.Ordinal);
    }

}

