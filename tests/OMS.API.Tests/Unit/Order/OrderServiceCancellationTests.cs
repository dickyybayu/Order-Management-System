namespace OMS.API.Tests.Unit;

public sealed class OrderServiceCancellationTests : TestBase
{
    [Fact]
    public async Task OrderCancelEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/cancel",
            CreateJsonContent(new CancelOrderRequest("Customer requested cancellation.")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    [InlineData(SystemRoleNames.SalesOperator)]
    public async Task AuthorizedRolesCanReachCancelEndpoint(string roleName)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/cancel",
            CreateJsonContent(new CancelOrderRequest(" Customer requested cancellation. ")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(nameof(OrderStatus.Cancelled), body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelOrderRejectsMissingOrBlankReason(string reason)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/cancel",
            CreateJsonContent(new CancelOrderRequest(reason)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CancelOrderRequest.Reason), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task CancelOrderRejectsTooLongReason()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            $"/api/v1/orders/{Guid.NewGuid()}/cancel",
            CreateJsonContent(new CancelOrderRequest(new string('A', 501))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin, OrderStatus.Pending)]
    [InlineData(SystemRoleNames.Admin, OrderStatus.Processing)]
    [InlineData(SystemRoleNames.Supervisor, OrderStatus.Pending)]
    [InlineData(SystemRoleNames.Supervisor, OrderStatus.Processing)]
    [InlineData(SystemRoleNames.SalesOperator, OrderStatus.Pending)]
    [InlineData(SystemRoleNames.SalesOperator, OrderStatus.Processing)]
    public async Task EligibleOrdersCanBeCancelledByAllowedRoles(string roleName, OrderStatus initialStatus)
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-TEST-0001",
            status: initialStatus);
        var product = repository.Products.Single();
        var stockBeforeCancellation = product.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, roleName, repository.CurrentUser.Id, notificationQueue);

        var response = await service.CancelAsync(
            order.Id,
            new CancelOrderRequest(" Customer requested cancellation. "),
            CancellationToken.None);

        var history = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Cancelled, response.Status);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAtUtc);
        Assert.NotNull(order.UpdatedAtUtc);
        Assert.Equal(stockBeforeCancellation + order.Items.Single().Quantity, product.Stock);
        Assert.Equal(initialStatus, history.FromStatus);
        Assert.Equal(OrderStatus.Cancelled, history.ToStatus);
        Assert.Equal(repository.CurrentUser.Id, history.ChangedByUserId);
        Assert.Equal("Customer requested cancellation.", history.Note);
        Assert.NotEqual(default, history.ChangedAtUtc);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
        Assert.Equal(initialStatus, notificationQueue.LastFromStatus);
        Assert.Equal(OrderStatus.Cancelled, notificationQueue.LastToStatus);
    }


    [Fact]
    public async Task SalesOperatorCannotCancelAnotherUsersOrderAndReceivesNotFound()
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(
            repository,
            SystemRoleNames.SalesOperator,
            repository.CurrentUser.Id,
            notificationQueue);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CancelAsync(
                otherOrder.Id,
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, otherOrder.Status);
        Assert.Empty(otherOrder.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanCancelAnotherUsersEligibleOrder(string roleName)
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        var service = CreateOrderService(repository, roleName, repository.CurrentUser.Id);

        var response = await service.CancelAsync(
            otherOrder.Id,
            new CancelOrderRequest("Customer requested cancellation."),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, response.Status);
        Assert.Equal(OrderStatus.Cancelled, otherOrder.Status);
    }


    [Fact]
    public async Task CancelMissingOrderReturnsNotFound()
    {
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(new FakeOrderRepository(), notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CancelAsync(
                Guid.NewGuid(),
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task CancellationRestoresMultipleOrderItemsExactlyOnce()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var firstProduct = repository.Products.Single();
        var secondProduct = repository.AddProduct("XYZ-789", "Wrench", stock: 7);
        repository.AddOrderItem(order, secondProduct, quantity: 3);
        var firstStockBefore = firstProduct.Stock;
        var secondStockBefore = secondProduct.Stock;
        var service = CreateOrderService(repository);

        await service.CancelAsync(
            order.Id,
            new CancelOrderRequest("Customer requested cancellation."),
            CancellationToken.None);

        Assert.Equal(firstStockBefore + 2, firstProduct.Stock);
        Assert.Equal(secondStockBefore + 3, secondProduct.Stock);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelAsync(
                order.Id,
                new CancelOrderRequest("Customer requested cancellation again."),
                CancellationToken.None));

        Assert.Equal(firstStockBefore + 2, firstProduct.Stock);
        Assert.Equal(secondStockBefore + 3, secondProduct.Stock);
    }


    [Fact]
    public async Task CancellationWriteFailureRollsBackOrderHistoryAndStock()
    {
        var repository = new FakeOrderRepository { ThrowOnSave = true };
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var product = repository.Products.Single();
        var stockBeforeCancellation = product.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelAsync(
                order.Id,
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.CancelledAtUtc);
        Assert.Null(order.UpdatedAtUtc);
        Assert.Equal(stockBeforeCancellation, product.Stock);
        Assert.Empty(order.StatusHistory);
        Assert.False(repository.TransactionCommitted);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task CancellationFailureDuringStockRestorationRollsBackAllChanges()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var firstProduct = repository.Products.Single();
        var secondProduct = repository.AddProduct("XYZ-789", "Wrench", stock: 7);
        repository.AddOrderItem(order, secondProduct, quantity: 3);
        var brokenItem = order.Items.Last();
        brokenItem.Product = null;
        var firstStockBefore = firstProduct.Stock;
        var secondStockBefore = secondProduct.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelAsync(
                order.Id,
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.CancelledAtUtc);
        Assert.Equal(firstStockBefore, firstProduct.Stock);
        Assert.Equal(secondStockBefore, secondProduct.Stock);
        Assert.Empty(order.StatusHistory);
        Assert.False(repository.TransactionCommitted);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task CancellationConcurrencyConflictReturnsConflictAndDoesNotNotify()
    {
        var repository = new FakeOrderRepository { ThrowConcurrencyOnSave = true };
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var product = repository.Products.Single();
        var stockBeforeCancellation = product.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelAsync(
                order.Id,
                new CancelOrderRequest("Customer requested cancellation."),
                CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.CancelledAtUtc);
        Assert.Equal(stockBeforeCancellation, product.Stock);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task NotificationFailureDoesNotUndoCancellation()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var product = repository.Products.Single();
        var stockBeforeCancellation = product.Stock;
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted)
        {
            ThrowOnEnqueue = true
        };
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        var response = await service.CancelAsync(
            order.Id,
            new CancelOrderRequest("Customer requested cancellation."),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, response.Status);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAtUtc);
        Assert.Equal(stockBeforeCancellation + order.Items.Single().Quantity, product.Stock);
        Assert.Single(order.StatusHistory);
        Assert.True(repository.TransactionCommitted);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
    }

}

