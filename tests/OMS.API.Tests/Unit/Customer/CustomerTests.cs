namespace OMS.API.Tests.Unit;

public sealed class CustomerTests : TestBase
{
    [Fact]
    public void CustomerEntityIsMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var customerEntity = dbContext.Model.FindEntityType(typeof(Customer));

        Assert.NotNull(customerEntity);
        Assert.Equal("Customers", customerEntity.GetTableName());
        Assert.Equal(150, customerEntity.FindProperty(nameof(Customer.Name))?.GetMaxLength());
        Assert.Equal(255, customerEntity.FindProperty(nameof(Customer.Email))?.GetMaxLength());
        Assert.Equal(30, customerEntity.FindProperty(nameof(Customer.Phone))?.GetMaxLength());
        Assert.Equal(500, customerEntity.FindProperty(nameof(Customer.ShippingAddress))?.GetMaxLength());
        Assert.False(customerEntity.FindProperty(nameof(Customer.Name))?.IsNullable);
        Assert.False(customerEntity.FindProperty(nameof(Customer.Email))?.IsNullable);
        Assert.True(customerEntity.FindProperty(nameof(Customer.Phone))?.IsNullable);
        Assert.False(customerEntity.FindProperty(nameof(Customer.ShippingAddress))?.IsNullable);
        Assert.False(customerEntity.FindProperty(nameof(Customer.IsActive))?.IsNullable);
        Assert.Contains(
            customerEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Customer.Email));
    }


    [Fact]
    public void CustomerEmailIsNormalizedAndStringsAreTrimmed()
    {
        var customer = new Customer
        {
            Name = "  Jane Buyer  ",
            Email = "  Jane@Example.COM  ",
            Phone = "  12345  ",
            ShippingAddress = "  Main Street  "
        };

        customer.NormalizeForStorage();

        Assert.Equal("Jane Buyer", customer.Name);
        Assert.Equal("jane@example.com", customer.Email);
        Assert.Equal("12345", customer.Phone);
        Assert.Equal("Main Street", customer.ShippingAddress);
    }


    [Fact]
    public async Task CustomerReadEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new CustomerApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AuthenticatedUserCanListAndGetCustomers()
    {
        await using var factory = new CustomerApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Supervisor);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Supervisor);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/customers");
        var getResponse = await client.GetAsync($"/api/v1/customers/{CustomerApplicationFactory.CustomerId}");
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("jane@example.com", listBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Customer.NormalizeForStorage), listBody, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData(SystemRoleNames.Admin)]
    [InlineData(SystemRoleNames.SalesOperator)]
    public async Task AdminAndSalesOperatorCanWriteCustomers(string roleName)
    {
        await using var factory = new CustomerApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(roleName);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, roleName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = CreateJsonContent(new CreateCustomerRequest("Jane Buyer", "jane@example.com", null, "Main Street"))
        };

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}. Body: {responseBody}");
    }


    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task SupervisorCannotWriteCustomers(string method)
    {
        await using var factory = new CustomerApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Supervisor);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Supervisor);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
            {
                Content = CreateJsonContent(new CreateCustomerRequest("Jane Buyer", "jane@example.com", null, "Main Street"))
            },
            "PUT" => new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{CustomerApplicationFactory.CustomerId}")
            {
                Content = CreateJsonContent(new UpdateCustomerRequest("Jane Buyer", "jane@example.com", null, "Main Street"))
            },
            _ => new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/customers/{CustomerApplicationFactory.CustomerId}/status")
            {
                Content = CreateJsonContent(new UpdateCustomerStatusRequest(false))
            }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task CustomerServiceCreatesUpdatesActivatesAndDeactivates()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);

        var created = await service.CreateAsync(
            new CreateCustomerRequest(" Jane Buyer ", " Jane@Example.COM ", " 12345 ", " Main Street "),
            CancellationToken.None);
        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateCustomerRequest("John Buyer", "john@example.com", null, "Second Street"),
            CancellationToken.None);
        var deactivated = await service.UpdateStatusAsync(created.Id, new UpdateCustomerStatusRequest(false), CancellationToken.None);
        var activated = await service.UpdateStatusAsync(created.Id, new UpdateCustomerStatusRequest(true), CancellationToken.None);

        Assert.Equal("Jane Buyer", created.Name);
        Assert.Equal("jane@example.com", created.Email);
        Assert.Equal("12345", created.Phone);
        Assert.Equal("Main Street", created.ShippingAddress);
        Assert.Equal("john@example.com", updated.Email);
        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }


    [Fact]
    public async Task DuplicateCustomerEmailReturnsConflict()
    {
        var repository = new FakeCustomerRepository();
        repository.Customers.Add(repository.CreateCustomer("Jane Buyer", "jane@example.com"));
        var service = new CustomerService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateCustomerRequest("Other", " JANE@example.com ", null, "Main Street"),
                CancellationToken.None));
    }


    [Fact]
    public async Task InvalidCustomerEmailIsRejectedByValidation()
    {
        await using var factory = new CustomerApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = CreateJsonContent(new CreateCustomerRequest("Jane Buyer", "not-an-email", null, "Main Street"))
        };

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateCustomerRequest.Email), body, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task MissingCustomerReturnsNotFound()
    {
        var service = new CustomerService(new FakeCustomerRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateAsync(
                Guid.NewGuid(),
                new UpdateCustomerRequest("Jane Buyer", "jane@example.com", null, "Main Street"),
                CancellationToken.None));
    }


    [Fact]
    public async Task CustomerSearchFilterSortingAndPaginationWork()
    {
        var repository = new FakeCustomerRepository();
        repository.Customers.Add(repository.CreateCustomer("Zeta Buyer", "zeta@example.com", isActive: true));
        repository.Customers.Add(repository.CreateCustomer("Alpha Buyer", "alpha@example.com", isActive: true));
        repository.Customers.Add(repository.CreateCustomer("Inactive Buyer", "inactive@example.com", isActive: false));
        var service = new CustomerService(repository);

        var response = await service.ListAsync(
            new CustomerListRequest
            {
                Search = "buyer",
                IsActive = true,
                SortBy = "email",
                SortDirection = SortDirection.Asc,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, response.Pagination.TotalItems);
        Assert.Single(response.Items);
        Assert.Equal("alpha@example.com", response.Items.Single().Email);
    }


    [Fact]
    public async Task CustomerListRejectsUnsupportedSortField()
    {
        var service = new CustomerService(new FakeCustomerRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(new CustomerListRequest { SortBy = "phone" }, CancellationToken.None));
    }


    [Fact]
    public async Task OrderServiceRejectsInactiveOrMissingCustomer()
    {
        var inactiveRepository = new FakeOrderRepository { UseInactiveCustomer = true };
        var inactiveService = CreateOrderService(inactiveRepository);
        var missingRepository = new FakeOrderRepository { CustomerMissing = true };
        var missingService = CreateOrderService(missingRepository);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => inactiveService.CreateAsync(CreateValidOrderRequest(inactiveRepository), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => missingService.CreateAsync(CreateValidOrderRequest(missingRepository), CancellationToken.None));
    }


    [Fact]
    public async Task SalesOperatorScopeCannotBeBypassedByCustomerFilter()
    {
        var repository = new FakeOrderRepository();
        var otherCustomerId = Guid.NewGuid();
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", customerId: repository.ActiveCustomer.Id);
        repository.CreatePersistedOrder(repository.OtherUser.Id, "ORD-TEST-0002", customerId: otherCustomerId);
        var service = CreateOrderService(repository, SystemRoleNames.SalesOperator, repository.CurrentUser.Id);

        var response = await service.ListAsync(
            new OrderQueryRequest { CustomerId = otherCustomerId },
            CancellationToken.None);

        Assert.Empty(response.Items);
        Assert.Equal(0, response.Pagination.TotalItems);
        Assert.Equal(repository.CurrentUser.Id, repository.LastListScopeCreatedByUserId);
    }


    [Fact]
    public async Task OrderListCanFilterByCustomerId()
    {
        var repository = new FakeOrderRepository();
        var customerId = Guid.NewGuid();
        var matchingOrder = repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0001", customerId: customerId);
        repository.CreatePersistedOrder(repository.CurrentUser.Id, "ORD-TEST-0002", customerId: Guid.NewGuid());
        var service = CreateOrderService(repository);

        var response = await service.ListAsync(
            new OrderQueryRequest { CustomerId = customerId },
            CancellationToken.None);

        var order = Assert.Single(response.Items);
        Assert.Equal(matchingOrder.Id, order.Id);
    }

}

