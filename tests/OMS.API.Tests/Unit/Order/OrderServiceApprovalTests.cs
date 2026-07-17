namespace OMS.API.Tests.Unit;

public sealed class OrderServiceApprovalTests : TestBase
{
    [Fact]
    public async Task OrderApproveEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/approve", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SalesOperatorCannotApproveOrder()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/approve", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanApproveOrder(string roleName)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync($"/api/v1/orders/{Guid.NewGuid()}/approve", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(nameof(OrderStatus.Processing), body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ApproveMissingOrderReturnsNotFound()
    {
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(new FakeOrderRepository(), notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.ApproveAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OnlyPendingOrdersCanBeApproved(OrderStatus status)
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", status: status);
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.ApproveAsync(order.Id, CancellationToken.None));

        Assert.Equal(status, order.Status);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task ApprovalWriteFailureRollsBackStatusHistoryAndNotification()
    {
        var repository = new FakeOrderRepository { ThrowOnSave = true };
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted);
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(order.Id, CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.UpdatedAtUtc);
        Assert.Empty(order.StatusHistory);
        Assert.False(repository.TransactionCommitted);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task ApprovalConcurrencyConflictReturnsConflictAndDoesNotNotify()
    {
        var repository = new FakeOrderRepository { ThrowConcurrencyOnSave = true };
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var notificationQueue = new FakeOrderStatusNotificationQueue();
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.ApproveAsync(order.Id, CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(order.StatusHistory);
        Assert.Equal(0, notificationQueue.CallCount);
    }


    [Fact]
    public async Task NotificationFailureDoesNotUndoApproval()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var notificationQueue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted)
        {
            ThrowOnEnqueue = true
        };
        var service = CreateOrderService(repository, notificationQueue: notificationQueue);

        var response = await service.ApproveAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Processing, response.Status);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Single(order.StatusHistory);
        Assert.True(repository.TransactionCommitted);
        Assert.Equal(1, notificationQueue.CallCount);
        Assert.True(notificationQueue.WasCommittedWhenCalled);
    }

}

