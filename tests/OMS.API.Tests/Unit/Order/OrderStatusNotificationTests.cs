namespace OMS.API.Tests.Unit;

public sealed class OrderStatusNotificationTests : TestBase
{
    [Fact]
    public Task OrderStatusNotificationQueueUsesCoravelQueueWithSafePayload()
    {
        var queue = new FakeCoravelQueue();
        var notificationQueue = new CoravelOrderStatusNotificationQueue(
            queue,
            NullLogger<CoravelOrderStatusNotificationQueue>.Instance);
        var orderId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        return AssertQueuePayloadAsync();

        async Task AssertQueuePayloadAsync()
        {
            await notificationQueue.EnqueueStatusChangedAsync(
                orderId,
                "ORD-TEST-0001",
                OrderStatus.Pending,
                OrderStatus.Processing,
                actorId,
                CancellationToken.None);

            var payload = Assert.IsType<OrderStatusNotificationPayload>(queue.LastPayload);
            Assert.Equal(orderId, payload.OrderId);
            Assert.Equal("ORD-TEST-0001", payload.OrderNumber);
            Assert.Equal(OrderStatus.Pending, payload.PreviousStatus);
            Assert.Equal(OrderStatus.Processing, payload.NewStatus);
            Assert.Equal(actorId, payload.ChangedByUserId);
            Assert.True(payload.QueuedAtUtc <= DateTime.UtcNow);
            Assert.DoesNotContain(
                typeof(OrderStatusNotificationPayload).GetProperties(),
                property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        }
    }


    [Fact]
    public async Task OrderStatusNotificationQueueFailureBubblesToOrderServiceWithoutUndoingCommittedTransition()
    {
        var repository = new FakeOrderRepository();
        var order = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var queue = new FakeOrderStatusNotificationQueue(() => repository.TransactionCommitted)
        {
            ThrowOnEnqueue = true
        };
        var service = CreateOrderService(repository, notificationQueue: queue);

        var response = await service.ApproveAsync(order.Id, CancellationToken.None);

        Assert.Equal(OrderStatus.Processing, response.Status);
        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.True(repository.TransactionCommitted);
        Assert.Equal(1, queue.CallCount);
    }


    [Fact]
    public void OrderStatusNotificationInvocableLogsSafeExecutionDetails()
    {
        var logger = new TestLogger<OrderStatusNotificationJob>();
        var payload = new OrderStatusNotificationPayload(
            Guid.NewGuid(),
            "ORD-TEST-0001",
            OrderStatus.Pending,
            OrderStatus.Processing,
            Guid.NewGuid(),
            DateTime.UtcNow);
        var job = new OrderStatusNotificationJob(logger)
        {
            Payload = payload
        };

        job.Invoke();

        Assert.Contains(logger.Entries, entry =>
            entry.Message.Contains("started", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Entries, entry =>
            entry.Message.Contains("finished", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Entries, entry =>
            entry.Message.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase) ||
            entry.Message.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
            entry.Message.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
    }

}

