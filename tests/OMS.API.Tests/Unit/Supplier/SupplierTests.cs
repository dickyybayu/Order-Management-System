namespace OMS.API.Tests.Unit;

public sealed class SupplierTests : TestBase
{
    [Fact]
    public void SupplierEntityIsMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var supplierEntity = dbContext.Model.FindEntityType(typeof(Supplier));

        Assert.NotNull(supplierEntity);
        Assert.Equal("Suppliers", supplierEntity.GetTableName());
        Assert.Equal(150, supplierEntity.FindProperty(nameof(Supplier.Name))?.GetMaxLength());
        Assert.Equal(255, supplierEntity.FindProperty(nameof(Supplier.Email))?.GetMaxLength());
        Assert.Equal(30, supplierEntity.FindProperty(nameof(Supplier.Phone))?.GetMaxLength());
        Assert.Equal(500, supplierEntity.FindProperty(nameof(Supplier.Address))?.GetMaxLength());
        Assert.False(supplierEntity.FindProperty(nameof(Supplier.Name))?.IsNullable);
        Assert.True(supplierEntity.FindProperty(nameof(Supplier.Email))?.IsNullable);
        Assert.True(supplierEntity.FindProperty(nameof(Supplier.Phone))?.IsNullable);
        Assert.True(supplierEntity.FindProperty(nameof(Supplier.Address))?.IsNullable);
        Assert.False(supplierEntity.FindProperty(nameof(Supplier.IsActive))?.IsNullable);
        Assert.False(supplierEntity.FindProperty(nameof(Supplier.CreatedAtUtc))?.IsNullable);
        Assert.True(supplierEntity.FindProperty(nameof(Supplier.UpdatedAtUtc))?.IsNullable);
    }


    [Fact]
    public void SupplierStringFieldsAreTrimmedBeforeStorage()
    {
        var supplier = new Supplier
        {
            Name = "  Main Supplier  ",
            Email = "  supplier@example.com  ",
            Phone = "  12345  ",
            Address = "  Warehouse  "
        };

        supplier.TrimStringFieldsForStorage();

        Assert.Equal("Main Supplier", supplier.Name);
        Assert.Equal("supplier@example.com", supplier.Email);
        Assert.Equal("12345", supplier.Phone);
        Assert.Equal("Warehouse", supplier.Address);
    }


    [Fact]
    public async Task SupplierReadEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new SupplierApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/suppliers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AuthenticatedUserCanListAndGetSuppliers()
    {
        await using var factory = new SupplierApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/suppliers");
        var getResponse = await client.GetAsync($"/api/v1/suppliers/{SupplierApplicationFactory.SupplierId}");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var getBody = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Main Supplier", listBody, StringComparison.Ordinal);
        Assert.Contains("Main Supplier", getBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Supplier.TrimStringFieldsForStorage), listBody, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task NonAdminCannotWriteSuppliers(string method)
    {
        await using var factory = new SupplierApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        using var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/suppliers")
            {
                Content = CreateJsonContent(new CreateSupplierRequest("Supplier", null, null, null))
            },
            "PUT" => new HttpRequestMessage(
                HttpMethod.Put,
                $"/api/v1/suppliers/{SupplierApplicationFactory.SupplierId}")
            {
                Content = CreateJsonContent(new UpdateSupplierRequest("Supplier", null, null, null))
            },
            _ => new HttpRequestMessage(
                HttpMethod.Patch,
                $"/api/v1/suppliers/{SupplierApplicationFactory.SupplierId}/status")
            {
                Content = CreateJsonContent(new UpdateSupplierStatusRequest(false))
            }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AdminCanCreateUpdateActivateAndDeactivateSupplier()
    {
        var repository = new FakeSupplierRepository();
        var service = new SupplierService(repository);

        var created = await service.CreateAsync(
            new CreateSupplierRequest(
                "  Main Supplier  ",
                "  supplier@example.com  ",
                "  12345  ",
                "  Warehouse  "),
            CancellationToken.None);
        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateSupplierRequest(
                "Updated Supplier",
                "updated@example.com",
                "67890",
                "Updated Address"),
            CancellationToken.None);
        var deactivated = await service.UpdateStatusAsync(
            created.Id,
            new UpdateSupplierStatusRequest(false),
            CancellationToken.None);
        var activated = await service.UpdateStatusAsync(
            created.Id,
            new UpdateSupplierStatusRequest(true),
            CancellationToken.None);

        Assert.Equal("Main Supplier", created.Name);
        Assert.Equal("supplier@example.com", created.Email);
        Assert.Equal("12345", created.Phone);
        Assert.Equal("Warehouse", created.Address);
        Assert.Equal("Updated Supplier", updated.Name);
        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }


    [Fact]
    public void SupplierEmailValidationMetadataIsDefinedOnRecordConstructorParameter()
    {
        var constructor = Assert.Single(typeof(CreateSupplierRequest).GetConstructors());
        var emailParameter = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == nameof(CreateSupplierRequest.Email));

        Assert.Contains(emailParameter.GetCustomAttributes(), attribute => attribute is EmailAddressAttribute);
    }


    [Fact]
    public async Task MissingSupplierReturnsNotFound()
    {
        var service = new SupplierService(new FakeSupplierRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateAsync(
                Guid.NewGuid(),
                new UpdateSupplierRequest("Supplier", null, null, null),
                CancellationToken.None));
    }


    [Fact]
    public async Task SupplierSearchSortingAndPaginationWork()
    {
        var repository = new FakeSupplierRepository();
        repository.Suppliers.Add(repository.CreateSupplier("Zeta Supplier", "zeta@example.com", "222"));
        repository.Suppliers.Add(repository.CreateSupplier("Alpha Supplier", "alpha@example.com", "111"));
        repository.Suppliers.Add(repository.CreateSupplier("Office Vendor", "vendor@example.com", "333"));
        var service = new SupplierService(repository);

        var response = await service.ListAsync(
            new SupplierListRequest
            {
                Search = "Supplier",
                SortBy = "name",
                SortDirection = SortDirection.Asc,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, response.Pagination.TotalItems);
        Assert.Single(response.Items);
        Assert.Equal("Alpha Supplier", response.Items.Single().Name);
    }


    [Fact]
    public async Task SupplierListRejectsUnsupportedSortField()
    {
        var service = new SupplierService(new FakeSupplierRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(
                new SupplierListRequest { SortBy = "address" },
                CancellationToken.None));
    }

}

