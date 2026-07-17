namespace OMS.API.Tests.Unit;

public sealed class OrderServiceQueryTests : TestBase
{
    [Fact]
    public async Task OrderListEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task OrderGetByIdEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AdminAndSupervisorCanListAllOrders()
    {
        var repository = new FakeOrderRepository();
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", DateTime.UtcNow.AddMinutes(-1));
        repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002", DateTime.UtcNow);

        var adminService = CreateOrderService(repository, SystemRoleNames.Admin);
        var supervisorService = CreateOrderService(repository, SystemRoleNames.Supervisor);

        var adminResponse = await adminService.ListAsync(new OrderQueryRequest(), CancellationToken.None);
        var supervisorResponse = await supervisorService.ListAsync(new OrderQueryRequest(), CancellationToken.None);

        Assert.Equal(2, adminResponse.Pagination.TotalItems);
        Assert.Equal(2, supervisorResponse.Pagination.TotalItems);
        Assert.Null(repository.LastListScopeCreatedByUserId);
    }


    [Fact]
    public async Task SalesOperatorSeesOnlyOwnedOrders()
    {
        var repository = new FakeOrderRepository();
        var ownedOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", DateTime.UtcNow);
        repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002", DateTime.UtcNow.AddMinutes(1));
        var service = CreateOrderService(repository, SystemRoleNames.SalesOperator, repository.CurrentUser.Id);

        var response = await service.ListAsync(new OrderQueryRequest(), CancellationToken.None);

        var order = Assert.Single(response.Items);
        Assert.Equal(ownedOrder.Id, order.Id);
        Assert.Equal(repository.CurrentUser.Id, repository.LastListScopeCreatedByUserId);
    }


    [Fact]
    public async Task SalesOperatorCannotRetrieveAnotherUsersOrder()
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        var service = CreateOrderService(repository, SystemRoleNames.SalesOperator, repository.CurrentUser.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(otherOrder.Id, CancellationToken.None));

        Assert.Equal(repository.CurrentUser.Id, repository.LastGetScopeCreatedByUserId);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanRetrieveAnyOrder(string roleName)
    {
        var repository = new FakeOrderRepository();
        var otherOrder = repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002");
        var service = CreateOrderService(repository, roleName);

        var response = await service.GetByIdAsync(otherOrder.Id, CancellationToken.None);

        Assert.Equal(otherOrder.Id, response.Id);
        Assert.Null(repository.LastGetScopeCreatedByUserId);
    }


    [Fact]
    public async Task MissingOrderReturnsNotFound()
    {
        var service = CreateOrderService(new FakeOrderRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }


    [Fact]
    public async Task OrderReadResponsesIncludeSafeSnapshots()
    {
        var repository = new FakeOrderRepository();
        var persistedOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001");
        var service = CreateOrderService(repository);

        var listResponse = await service.ListAsync(new OrderQueryRequest(), CancellationToken.None);
        var detailResponse = await service.GetByIdAsync(persistedOrder.Id, CancellationToken.None);
        var listOrder = Assert.Single(listResponse.Items);
        var detailItem = Assert.Single(detailResponse.Items);

        Assert.Equal("ORD-TEST-0001", listOrder.OrderNumber);
        Assert.Equal("Jane Buyer", detailResponse.Customer.Name);
        Assert.Equal("Admin User", detailResponse.CreatedBy.Name);
        Assert.Equal("TRK-123", detailResponse.TrackingNumber);
        Assert.Equal("IDR", detailResponse.CurrencyCode);
        Assert.Equal("ABC-123", detailItem.ProductSku);
        Assert.Equal("Hammer", detailItem.ProductName);
        Assert.Equal(10m, detailItem.UnitPrice);
        Assert.Equal(20m, detailItem.LineTotal);
    }


    [Fact]
    public async Task OrderReadEndpointDoesNotReturnPasswordHash()
    {
        await using var factory = new OrderApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/orders");
        var detailResponse = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var detailBody = await detailResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.DoesNotContain(nameof(User.PasswordHash), listBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(User.PasswordHash), detailBody, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.Supervisor)]
    public async Task AdminAndSupervisorCanFilterAllOrders(string roleName)
    {
        var repository = new FakeOrderRepository();
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", status: OrderStatus.Pending);
        var matchingOrder = repository.CreatePersistedOrder(
            repository.OtherUser.Id,
            "ORD-TEST-0002",
            status: OrderStatus.Shipped);
        var service = CreateOrderService(repository, roleName);

        var response = await service.ListAsync(
            new OrderQueryRequest { Status = nameof(OrderStatus.Shipped) },
            CancellationToken.None);

        var order = Assert.Single(response.Items);
        Assert.Equal(matchingOrder.Id, order.Id);
        Assert.Null(repository.LastListScopeCreatedByUserId);
    }


    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OrderListCanFilterByEachStatus(OrderStatus status)
    {
        var repository = new FakeOrderRepository();
        var matchingOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, $"ORD-{status}", status: status);
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-OTHER", status: NextStatus(status));
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(
            new OrderQueryRequest { Status = status.ToString() },
            CancellationToken.None);

        var order = Assert.Single(response.Items);
        Assert.Equal(matchingOrder.Id, order.Id);
    }


    [Fact]
    public async Task OrderListDateFiltersUseInclusiveUtcBoundaries()
    {
        var repository = new FakeOrderRepository();
        var from = new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-BEFORE", from.AddTicks(-1));
        var lowerBoundaryOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-FROM", from);
        var upperBoundaryOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TO", to);
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-AFTER", to.AddTicks(1));
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(
            new OrderQueryRequest { DateFrom = from, DateTo = to, SortBy = "createdAt", SortDirection = SortDirection.Asc },
            CancellationToken.None);

        Assert.Equal([lowerBoundaryOrder.Id, upperBoundaryOrder.Id], response.Items.Select(order => order.Id));
    }


    [Fact]
    public async Task OrderListRejectsInvalidDateRange()
    {
        var service = CreateOrderService(new FakeOrderRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(
                new OrderQueryRequest
                {
                    DateFrom = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
                    DateTo = new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc)
                },
                CancellationToken.None));
    }


    [Theory]
    [InlineData("passwordHash")]
    [InlineData("customer")]
    public async Task OrderListRejectsUnsupportedSortField(string sortBy)
    {
        var service = CreateOrderService(new FakeOrderRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(new OrderQueryRequest { SortBy = sortBy }, CancellationToken.None));
    }


    [Fact]
    public async Task OrderListRejectsUnsupportedStatus()
    {
        var service = CreateOrderService(new FakeOrderRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(new OrderQueryRequest { Status = "1" }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(new OrderQueryRequest { Status = "Archived" }, CancellationToken.None));
    }


    [Theory]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    [InlineData("orderNumber")]
    [InlineData("status")]
    [InlineData("totalAmount")]
    public async Task OrderListSupportsWhitelistedSortingInBothDirections(string sortBy)
    {
        var repository = new FakeOrderRepository();
        repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-B",
            new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc),
            OrderStatus.Shipped,
            updatedAtUtc: new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
            totalAmount: 30m);
        repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-A",
            new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc),
            OrderStatus.Pending,
            updatedAtUtc: new DateTime(2026, 7, 17, 11, 0, 0, DateTimeKind.Utc),
            totalAmount: 10m);
        var service = CreateOrderService(repository);

        var ascending = await service.ListAsync(
            new OrderQueryRequest { SortBy = sortBy, SortDirection = SortDirection.Asc },
            CancellationToken.None);
        var descending = await service.ListAsync(
            new OrderQueryRequest { SortBy = sortBy, SortDirection = SortDirection.Desc },
            CancellationToken.None);

        Assert.NotEqual(ascending.Items.First().Id, descending.Items.First().Id);
    }


    [Fact]
    public async Task OrderListDefaultSortingIsCreatedAtDescending()
    {
        var repository = new FakeOrderRepository();
        repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-OLD",
            new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc));
        var newestOrder = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-NEW",
            new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc));
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(new OrderQueryRequest(), CancellationToken.None);

        Assert.Equal(newestOrder.Id, response.Items.First().Id);
    }


    [Fact]
    public async Task OrderListPaginationMetadataIsCorrect()
    {
        var repository = new FakeOrderRepository();
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-1");
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-2");
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-3");
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(
            new OrderQueryRequest { Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Single(response.Items);
        Assert.Equal(2, response.Pagination.Page);
        Assert.Equal(2, response.Pagination.PageSize);
        Assert.Equal(3, response.Pagination.TotalItems);
        Assert.Equal(2, response.Pagination.TotalPages);
        Assert.True(response.Pagination.HasPreviousPage);
        Assert.False(response.Pagination.HasNextPage);
    }


    [Fact]
    public async Task OrderListEmptyResultReturnsValidMetadata()
    {
        var service = CreateOrderService(new FakeOrderRepository());

        var response = await service.ListAsync(
            new OrderQueryRequest { Status = nameof(OrderStatus.Delivered), Page = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.Empty(response.Items);
        Assert.Equal(0, response.Pagination.TotalItems);
        Assert.Equal(0, response.Pagination.TotalPages);
        Assert.False(response.Pagination.HasPreviousPage);
        Assert.False(response.Pagination.HasNextPage);
    }


    [Fact]
    public async Task OrderListCombinedFiltersWork()
    {
        var repository = new FakeOrderRepository();
        var customerId = Guid.NewGuid();
        var matchingOrder = repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-MATCH",
            new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc),
            OrderStatus.Processing,
            customerId: customerId);
        repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-WRONG-STATUS",
            new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc),
            OrderStatus.Pending,
            customerId: customerId);
        repository.CreatePersistedOrder(
            repository.CurrentUser.Id,
            "ORD-WRONG-CUSTOMER",
            new DateTime(2026, 7, 17, 9, 0, 0, DateTimeKind.Utc),
            OrderStatus.Processing,
            customerId: Guid.NewGuid());
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(
            new OrderQueryRequest
            {
                Status = nameof(OrderStatus.Processing),
                CustomerId = customerId,
                DateFrom = new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc),
                DateTo = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc)
            },
            CancellationToken.None);

        var order = Assert.Single(response.Items);
        Assert.Equal(matchingOrder.Id, order.Id);
    }


    [Fact]
    public void OrderRepositoryReadQueriesUseNoTrackingAndEagerLoading()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "OMS.API", "Infrastructure", "Repositories", "Order", "OrderRepository.cs"));

        Assert.Contains(".AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains(".Include(order => order.Customer)", source, StringComparison.Ordinal);
        Assert.Contains(".Include(order => order.CreatedByUser)", source, StringComparison.Ordinal);
        Assert.Contains(".Include(order => order.Items)", source, StringComparison.Ordinal);
        Assert.Contains("ApplyFilters(CreateReadQuery(createdByUserId), request)", source, StringComparison.Ordinal);
        Assert.Contains("ApplySorting(", source, StringComparison.Ordinal);
    }

}

