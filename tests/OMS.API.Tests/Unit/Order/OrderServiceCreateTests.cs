namespace OMS.API.Tests.Unit;

public sealed class OrderServiceCreateTests : TestBase
{
    [Fact]
    public async Task OrderCreateEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/orders", CreateJsonContent(CreateValidOrderRequest()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task SupervisorCannotCreateOrder()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Supervisor);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Supervisor);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync("/api/v1/orders", CreateJsonContent(CreateValidOrderRequest()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.SalesOperator)]
    public async Task AdminAndSalesOperatorCanCreateOrder(string roleName)
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync("/api/v1/orders", CreateJsonContent(CreateValidOrderRequest()));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("ORD-TEST-0001", body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(User.PasswordHash), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(Product.RowVersion), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task OrderServiceRejectsEmptyDuplicateAndInvalidQuantities()
    {
        var repository = new FakeOrderRepository();
        var service = CreateOrderService(repository);
        var productId = repository.Products.Single().Id;

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(
                new CreateOrderRequest(repository.ActiveCustomer.Id, "IDR", []),
                CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(
                new CreateOrderRequest(
                    repository.ActiveCustomer.Id,
                    "IDR",
                    [new CreateOrderItemRequest(productId, 1), new CreateOrderItemRequest(productId, 2)]),
                CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(
                new CreateOrderRequest(
                    repository.ActiveCustomer.Id,
                    "IDR",
                    [new CreateOrderItemRequest(productId, 0)]),
                CancellationToken.None));
    }


    [Fact]
    public async Task InsufficientStockReturnsBusinessRuleFailureAndChangesNothing()
    {
        var repository = new FakeOrderRepository();
        var service = CreateOrderService(repository);
        var product = repository.Products.Single();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(
                new CreateOrderRequest(
                    repository.ActiveCustomer.Id,
                    "IDR",
                    [new CreateOrderItemRequest(product.Id, product.Stock + 1)]),
                CancellationToken.None));

        Assert.Empty(repository.Orders);
        Assert.Equal(5, product.Stock);
    }


    [Fact]
    public async Task FailedWriteRollsBackOrderAndStockChanges()
    {
        var repository = new FakeOrderRepository { ThrowOnSave = true };
        var service = CreateOrderService(repository);
        var product = repository.Products.Single();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(CreateValidOrderRequest(repository), CancellationToken.None));

        Assert.Empty(repository.Orders);
        Assert.Equal(5, product.Stock);
    }


    [Fact]
    public void OrderNumberGeneratorProducesUniqueDocumentedFormat()
    {
        var generator = new OrderNumberGenerator();
        var now = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);

        var first = generator.Create(now);
        var second = generator.Create(now);

        Assert.StartsWith("ORD-20260717-", first, StringComparison.Ordinal);
        Assert.StartsWith("ORD-20260717-", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }


    [Fact]
    public async Task OrderServiceTranslatesConcurrencyConflictsToConflictException()
    {
        var repository = new FakeOrderRepository { ThrowConcurrencyOnSave = true };
        var service = CreateOrderService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(CreateValidOrderRequest(repository), CancellationToken.None));
    }


    [Fact]
    public async Task InvalidOrderRequestModelReturnsBadRequest()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsync(
            "/api/v1/orders",
            CreateJsonContent(new CreateOrderRequest(Guid.Empty, "ID", [])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}

