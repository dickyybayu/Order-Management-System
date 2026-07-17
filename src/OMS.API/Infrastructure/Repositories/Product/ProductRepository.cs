using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
using OMS.API.Domain.Product.Dtos;
using CategoryEntity = global::OMS.API.Models.Category;
using ProductEntity = global::OMS.API.Models.Product;
using SupplierEntity = global::OMS.API.Models.Supplier;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Product;

public sealed class ProductRepository(ApplicationDbContext dbContext) : IProductRepository
{
    public async Task<PaginatedResult<ProductEntity>> ListAsync(
        ProductListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var normalizedSkuSearch = ProductEntity.NormalizeSku(search);

            query = query.Where(product =>
                product.SKU.Contains(normalizedSkuSearch) ||
                product.Name.Contains(search));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == request.CategoryId.Value);
        }

        if (request.SupplierId.HasValue)
        {
            query = query.Where(product => product.SupplierId == request.SupplierId.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(product => product.IsActive == request.IsActive.Value);
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var products = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ProductEntity>(
            products,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.Supplier)
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public Task<ProductEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Products
            .Include(product => product.Category)
            .Include(product => product.Supplier)
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public Task<bool> SkuExistsAsync(
        string normalizedSku,
        Guid? excludingProductId,
        CancellationToken cancellationToken)
    {
        return dbContext.Products
            .AsNoTracking()
            .AnyAsync(
                product => product.SKU == normalizedSku &&
                    (!excludingProductId.HasValue || product.Id != excludingProductId.Value),
                cancellationToken);
    }

    public Task<CategoryEntity?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public Task<SupplierEntity?> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public async Task AddAsync(ProductEntity product, CancellationToken cancellationToken)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<ProductEntity> ApplySorting(
        IQueryable<ProductEntity> query,
        string? sortBy,
        SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "sku" => descending ? query.OrderByDescending(product => product.SKU) : query.OrderBy(product => product.SKU),
            "name" => descending ? query.OrderByDescending(product => product.Name) : query.OrderBy(product => product.Name),
            "price" => descending ? query.OrderByDescending(product => product.Price) : query.OrderBy(product => product.Price),
            "stock" => descending ? query.OrderByDescending(product => product.Stock) : query.OrderBy(product => product.Stock),
            "isactive" => descending ? query.OrderByDescending(product => product.IsActive) : query.OrderBy(product => product.IsActive),
            "updatedat" => descending ? query.OrderByDescending(product => product.UpdatedAtUtc) : query.OrderBy(product => product.UpdatedAtUtc),
            _ => descending ? query.OrderByDescending(product => product.CreatedAtUtc) : query.OrderBy(product => product.CreatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
