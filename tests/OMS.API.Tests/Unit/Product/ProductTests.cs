namespace OMS.API.Tests.Unit;

public sealed class ProductTests : TestBase
{
    [Fact]
    public void ProductEntityIsMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var productEntity = dbContext.Model.FindEntityType(typeof(Product));

        Assert.NotNull(productEntity);
        Assert.Equal("Products", productEntity.GetTableName());
        Assert.Equal(50, productEntity.FindProperty(nameof(Product.SKU))?.GetMaxLength());
        Assert.Equal(150, productEntity.FindProperty(nameof(Product.Name))?.GetMaxLength());
        Assert.Equal(30, productEntity.FindProperty(nameof(Product.Unit))?.GetMaxLength());
        Assert.False(productEntity.FindProperty(nameof(Product.SKU))?.IsNullable);
        Assert.False(productEntity.FindProperty(nameof(Product.Name))?.IsNullable);
        Assert.False(productEntity.FindProperty(nameof(Product.Unit))?.IsNullable);
        Assert.Equal("decimal(18,2)", productEntity.FindProperty(nameof(Product.Price))?.GetColumnType());
        Assert.True(productEntity.FindProperty(nameof(Product.RowVersion))?.IsConcurrencyToken);
        Assert.Contains(
            productEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Product.SKU));

        var categoryFk = productEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(Product.CategoryId));
        var supplierFk = productEntity.GetForeignKeys().Single(fk => fk.Properties.Single().Name == nameof(Product.SupplierId));
        Assert.Equal(DeleteBehavior.Restrict, categoryFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, supplierFk.DeleteBehavior);
        Assert.False(productEntity.FindProperty(nameof(Product.CategoryId))?.IsNullable);
        Assert.True(productEntity.FindProperty(nameof(Product.SupplierId))?.IsNullable);
    }


    [Fact]
    public void ProductSkuIsNormalizedAndStringsAreTrimmed()
    {
        var product = new Product
        {
            SKU = "  abc-123  ",
            Name = "  Hammer  ",
            Unit = "  pcs  "
        };

        product.NormalizeForStorage();

        Assert.Equal("ABC-123", product.SKU);
        Assert.Equal("Hammer", product.Name);
        Assert.Equal("pcs", product.Unit);
    }


    [Fact]
    public async Task ProductReadEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new ProductApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AuthenticatedUserCanListAndGetProducts()
    {
        await using var factory = new ProductApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/products");
        var getResponse = await client.GetAsync($"/api/v1/products/{ProductApplicationFactory.ProductId}");
        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("ABC-123", listBody, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Product.RowVersion), listBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(Product.NormalizeForStorage), listBody, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task NonAdminCannotWriteProducts(string method)
    {
        await using var factory = new ProductApplicationFactory();
        using var client = factory.CreateClient();
        var user = CreateTokenUser(SystemRoleNames.SalesOperator);
        var token = new JwtTokenService(Options.Create(TestApplicationFactory.JwtOptions))
            .CreateAccessToken(user, SystemRoleNames.SalesOperator);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var request = method switch
        {
            "POST" => new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
            {
                Content = CreateJsonContent(new CreateProductRequest("ABC-123", "Hammer", "pcs", 10, 5, Guid.NewGuid(), null))
            },
            "PUT" => new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{ProductApplicationFactory.ProductId}")
            {
                Content = CreateJsonContent(new UpdateProductRequest("ABC-123", "Hammer", "pcs", 10, 5, Guid.NewGuid(), null))
            },
            _ => new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/products/{ProductApplicationFactory.ProductId}/status")
            {
                Content = CreateJsonContent(new UpdateProductStatusRequest(false))
            }
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    public async Task AdminCanCreateUpdateActivateAndDeactivateProduct()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var created = await service.CreateAsync(
            new CreateProductRequest(" abc-123 ", " Hammer ", " pcs ", 10.50m, 5, repository.ActiveCategory.Id, repository.ActiveSupplier.Id),
            CancellationToken.None);
        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateProductRequest("xyz-999", "Wrench", "box", 20m, 10, repository.ActiveCategory.Id, null),
            CancellationToken.None);
        var deactivated = await service.UpdateStatusAsync(created.Id, new UpdateProductStatusRequest(false), CancellationToken.None);
        var activated = await service.UpdateStatusAsync(created.Id, new UpdateProductStatusRequest(true), CancellationToken.None);

        Assert.Equal("ABC-123", created.SKU);
        Assert.Equal("Hammer", created.Name);
        Assert.Equal("pcs", created.Unit);
        Assert.Equal("XYZ-999", updated.SKU);
        Assert.Null(updated.Supplier);
        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }


    [Fact]
    public async Task DuplicateProductSkuReturnsConflict()
    {
        var repository = new FakeProductRepository();
        repository.Products.Add(repository.CreateProduct("ABC-123", "Hammer"));
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateProductRequest(" abc-123 ", "Other", "pcs", 10m, 1, repository.ActiveCategory.Id, null),
                CancellationToken.None));
    }


    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void InvalidProductPriceOrStockIsRejected(decimal price, int stock)
    {
        var request = new CreateProductRequest("ABC-123", "Hammer", "pcs", price, stock, Guid.NewGuid(), null);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
    }


    [Fact]
    public async Task ProductSearchFiltersSortingAndPaginationWork()
    {
        var repository = new FakeProductRepository();
        var otherCategory = repository.CreateCategory("Tools", isActive: true);
        repository.Categories.Add(otherCategory);
        repository.Products.Add(repository.CreateProduct("ZZZ-999", "Zeta Hammer", price: 30m, stock: 9));
        repository.Products.Add(repository.CreateProduct("AAA-111", "Alpha Hammer", price: 10m, stock: 3));
        repository.Products.Add(repository.CreateProduct("BBB-222", "Office Chair", category: otherCategory, supplier: null, isActive: false));
        var service = new ProductService(repository);

        var response = await service.ListAsync(
            new ProductListRequest
            {
                Search = "Hammer",
                CategoryId = repository.ActiveCategory.Id,
                SupplierId = repository.ActiveSupplier.Id,
                IsActive = true,
                SortBy = "sku",
                SortDirection = SortDirection.Asc,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, response.Pagination.TotalItems);
        Assert.Single(response.Items);
        Assert.Equal("AAA-111", response.Items.Single().SKU);
    }


    [Fact]
    public async Task ProductListRejectsUnsupportedSortField()
    {
        var service = new ProductService(new FakeProductRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(new ProductListRequest { SortBy = "category" }, CancellationToken.None));
    }


    [Fact]
    public async Task ProductConcurrencyConflictReturnsConflict()
    {
        var repository = new FakeProductRepository { ThrowConcurrencyOnSave = true };
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateProductRequest("ABC-123", "Hammer", "pcs", 10m, 1, repository.ActiveCategory.Id, null),
                CancellationToken.None));
    }


    [Fact]
    public async Task OrderServiceRejectsInactiveOrMissingProduct()
    {
        var inactiveRepository = new FakeOrderRepository { UseInactiveProduct = true };
        var inactiveService = CreateOrderService(inactiveRepository);
        var missingRepository = new FakeOrderRepository { ProductMissing = true };
        var missingService = CreateOrderService(missingRepository);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => inactiveService.CreateAsync(CreateValidOrderRequest(inactiveRepository), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => missingService.CreateAsync(CreateValidOrderRequest(missingRepository), CancellationToken.None));
    }

}

