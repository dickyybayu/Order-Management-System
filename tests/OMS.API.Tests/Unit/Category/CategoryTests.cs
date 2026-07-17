namespace OMS.API.Tests.Unit;

public sealed class CategoryTests : TestBase
{
    [Fact]
    public void CategoryEntityIsMappedAccordingToDatabaseDesign()
    {
        var dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=OMS;TrustServerCertificate=True;")
            .Options;
        using var dbContext = new ApplicationDbContext(dbContextOptions);

        var categoryEntity = dbContext.Model.FindEntityType(typeof(Category));

        Assert.NotNull(categoryEntity);
        Assert.Equal("Categories", categoryEntity.GetTableName());
        Assert.Equal(100, categoryEntity.FindProperty(nameof(Category.Name))?.GetMaxLength());
        Assert.Equal(500, categoryEntity.FindProperty(nameof(Category.Description))?.GetMaxLength());
        Assert.False(categoryEntity.FindProperty(nameof(Category.Name))?.IsNullable);
        Assert.True(categoryEntity.FindProperty(nameof(Category.Description))?.IsNullable);
        Assert.False(categoryEntity.FindProperty(nameof(Category.IsActive))?.IsNullable);
        Assert.Contains(
            categoryEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Category.Name));
    }


    [Fact]
    public void CategoryNameIsNormalizedByTrimmingWhitespace()
    {
        var category = new Category { Name = "  Hardware  " };

        category.NormalizeNameForStorage();

        Assert.Equal("Hardware", category.Name);
    }


    [Fact]
    public async Task CategoryReadEndpointReturnsUnauthorizedWithoutToken()
    {
        await using var factory = new CategoryApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task AdminCanCreateUpdateActivateAndDeactivateCategory()
    {
        var repository = new FakeCategoryRepository();
        var service = new CategoryService(repository);

        var created = await service.CreateAsync(
            new CreateCategoryRequest("  Hardware  ", " Equipment "),
            CancellationToken.None);
        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateCategoryRequest("Tools", "Tools and accessories"),
            CancellationToken.None);
        var deactivated = await service.UpdateStatusAsync(
            created.Id,
            new UpdateCategoryStatusRequest(false),
            CancellationToken.None);
        var activated = await service.UpdateStatusAsync(
            created.Id,
            new UpdateCategoryStatusRequest(true),
            CancellationToken.None);

        Assert.Equal("Hardware", created.Name);
        Assert.Equal("Equipment", created.Description);
        Assert.Equal("Tools", updated.Name);
        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }


    [Fact]
    public async Task DuplicateCategoryNameReturnsConflict()
    {
        var repository = new FakeCategoryRepository();
        repository.Categories.Add(repository.CreateCategory("Hardware"));
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateCategoryRequest(" Hardware ", null),
                CancellationToken.None));
    }


    [Fact]
    public async Task MissingCategoryReturnsNotFound()
    {
        var service = new CategoryService(new FakeCategoryRepository());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateAsync(
                Guid.NewGuid(),
                new UpdateCategoryRequest("Hardware", null),
                CancellationToken.None));
    }


    [Fact]
    public async Task CategorySearchSortingAndPaginationWork()
    {
        var repository = new FakeCategoryRepository();
        repository.Categories.Add(repository.CreateCategory("Zeta Hardware"));
        repository.Categories.Add(repository.CreateCategory("Alpha Hardware"));
        repository.Categories.Add(repository.CreateCategory("Office Supplies"));
        var service = new CategoryService(repository);

        var response = await service.ListAsync(
            new CategoryListRequest
            {
                Search = "Hardware",
                SortBy = "name",
                SortDirection = SortDirection.Asc,
                Page = 1,
                PageSize = 1
            },
            CancellationToken.None);

        Assert.Equal(2, response.Pagination.TotalItems);
        Assert.Single(response.Items);
        Assert.Equal("Alpha Hardware", response.Items.Single().Name);
    }


    [Fact]
    public async Task CategoryListRejectsUnsupportedSortField()
    {
        var service = new CategoryService(new FakeCategoryRepository());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ListAsync(
                new CategoryListRequest { SortBy = "description" },
                CancellationToken.None));
    }


    [Fact]
    public async Task MissingCategoryOrSupplierReturnsNotFound()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateAsync(
                new CreateProductRequest("ABC-123", "Hammer", "pcs", 10m, 1, Guid.NewGuid(), null),
                CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateAsync(
                new CreateProductRequest("ABC-124", "Hammer", "pcs", 10m, 1, repository.ActiveCategory.Id, Guid.NewGuid()),
                CancellationToken.None));
    }


    [Fact]
    public async Task InactiveCategoryOrSupplierPreventsActiveProduct()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateProductRequest("ABC-123", "Hammer", "pcs", 10m, 1, repository.InactiveCategory.Id, null),
                CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(
                new CreateProductRequest("ABC-124", "Hammer", "pcs", 10m, 1, repository.ActiveCategory.Id, repository.InactiveSupplier.Id),
                CancellationToken.None));

        var inactiveProduct = repository.CreateProduct("ABC-125", "Inactive", isActive: false);
        inactiveProduct.CategoryId = repository.InactiveCategory.Id;
        inactiveProduct.Category = repository.InactiveCategory;
        repository.Products.Add(inactiveProduct);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateStatusAsync(inactiveProduct.Id, new UpdateProductStatusRequest(true), CancellationToken.None));
    }

}

