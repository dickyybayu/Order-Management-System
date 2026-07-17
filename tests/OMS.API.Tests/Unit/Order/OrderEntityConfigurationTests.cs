namespace OMS.API.Tests.Unit;

public sealed class OrderEntityConfigurationTests : TestBase
{
    [Fact]
    public void OrderEntitiesAreMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var orderEntity = dbContext.Model.FindEntityType(typeof(Order));
        var orderItemEntity = dbContext.Model.FindEntityType(typeof(OrderItem));
        var historyEntity = dbContext.Model.FindEntityType(typeof(OrderStatusHistory));

        Assert.NotNull(orderEntity);
        Assert.NotNull(orderItemEntity);
        Assert.NotNull(historyEntity);
        Assert.Equal("Orders", orderEntity.GetTableName());
        Assert.Equal("OrderItems", orderItemEntity.GetTableName());
        Assert.Equal("OrderStatusHistories", historyEntity.GetTableName());
        Assert.Equal(40, orderEntity.FindProperty(nameof(Order.OrderNumber))?.GetMaxLength());
        Assert.Equal(30, orderEntity.FindProperty(nameof(Order.Status))?.GetMaxLength());
        Assert.Equal(100, orderEntity.FindProperty(nameof(Order.TrackingNumber))?.GetMaxLength());
        Assert.Equal("char(3)", orderEntity.FindProperty(nameof(Order.CurrencyCode))?.GetColumnType());
        Assert.Equal("decimal(18,6)", orderEntity.FindProperty(nameof(Order.ExchangeRate))?.GetColumnType());
        Assert.Equal("decimal(18,2)", orderEntity.FindProperty(nameof(Order.Subtotal))?.GetColumnType());
        Assert.Equal("decimal(18,2)", orderEntity.FindProperty(nameof(Order.TotalAmount))?.GetColumnType());
        Assert.False(orderEntity.FindProperty(nameof(Order.OrderNumber))?.IsNullable);
        Assert.False(orderEntity.FindProperty(nameof(Order.Status))?.IsNullable);
        Assert.False(orderEntity.FindProperty(nameof(Order.CurrencyCode))?.IsNullable);
        Assert.False(orderEntity.FindProperty(nameof(Order.CreatedAtUtc))?.IsNullable);

        Assert.Equal(50, orderItemEntity.FindProperty(nameof(OrderItem.ProductSku))?.GetMaxLength());
        Assert.Equal(150, orderItemEntity.FindProperty(nameof(OrderItem.ProductName))?.GetMaxLength());
        Assert.Equal("decimal(18,2)", orderItemEntity.FindProperty(nameof(OrderItem.UnitPrice))?.GetColumnType());
        Assert.Equal("decimal(18,2)", orderItemEntity.FindProperty(nameof(OrderItem.LineTotal))?.GetColumnType());
        Assert.False(orderItemEntity.FindProperty(nameof(OrderItem.ProductSku))?.IsNullable);
        Assert.False(orderItemEntity.FindProperty(nameof(OrderItem.ProductName))?.IsNullable);

        Assert.Equal(30, historyEntity.FindProperty(nameof(OrderStatusHistory.FromStatus))?.GetMaxLength());
        Assert.Equal(30, historyEntity.FindProperty(nameof(OrderStatusHistory.ToStatus))?.GetMaxLength());
        Assert.Equal(500, historyEntity.FindProperty(nameof(OrderStatusHistory.Note))?.GetMaxLength());
        Assert.True(historyEntity.FindProperty(nameof(OrderStatusHistory.FromStatus))?.IsNullable);
        Assert.False(historyEntity.FindProperty(nameof(OrderStatusHistory.ToStatus))?.IsNullable);
        Assert.False(historyEntity.FindProperty(nameof(OrderStatusHistory.ChangedAtUtc))?.IsNullable);
    }


    [Fact]
    public void OrderEntitiesHaveRequiredIndexesAndDeleteBehaviors()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var orderEntity = dbContext.Model.FindEntityType(typeof(Order));
        var orderItemEntity = dbContext.Model.FindEntityType(typeof(OrderItem));
        var historyEntity = dbContext.Model.FindEntityType(typeof(OrderStatusHistory));

        Assert.NotNull(orderEntity);
        Assert.NotNull(orderItemEntity);
        Assert.NotNull(historyEntity);
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Order.OrderNumber));
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(Order.Status));
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(Order.CustomerId));
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(Order.CreatedByUserId));
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(Order.CreatedAtUtc));
        Assert.Contains(
            orderEntity.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Order.Status), nameof(Order.CustomerId), nameof(Order.CreatedAtUtc)]));

        var customerFk = orderEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(Order.CustomerId));
        var createdByUserFk = orderEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(Order.CreatedByUserId));
        var orderItemOrderFk = orderItemEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(OrderItem.OrderId));
        var orderItemProductFk = orderItemEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(OrderItem.ProductId));
        var historyOrderFk = historyEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(OrderStatusHistory.OrderId));
        var historyUserFk = historyEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(OrderStatusHistory.ChangedByUserId));

        Assert.Equal(DeleteBehavior.Restrict, customerFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, createdByUserFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, orderItemOrderFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, orderItemProductFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, historyOrderFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, historyUserFk.DeleteBehavior);
    }


    [Fact]
    public void OrderSnapshotAndStatusFieldsAreNormalizedBeforeStorage()
    {
        var order = new Order
        {
            OrderNumber = "  ORD-20260717-0001  ",
            TrackingNumber = "  JNE-123  ",
            CurrencyCode = " usd "
        };
        var item = new OrderItem
        {
            ProductSku = "  abc-123  ",
            ProductName = "  Hammer  "
        };
        var history = new OrderStatusHistory
        {
            Note = "  Initial creation  "
        };

        order.NormalizeForStorage();
        item.NormalizeForStorage();
        history.NormalizeForStorage();

        Assert.Equal("ORD-20260717-0001", order.OrderNumber);
        Assert.Equal("JNE-123", order.TrackingNumber);
        Assert.Equal("USD", order.CurrencyCode);
        Assert.Equal("ABC-123", item.ProductSku);
        Assert.Equal("Hammer", item.ProductName);
        Assert.Equal("Initial creation", history.Note);
    }

}

