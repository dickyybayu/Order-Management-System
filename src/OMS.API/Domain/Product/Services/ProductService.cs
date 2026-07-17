using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
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
using OMS.API.Domain.Auth.Services;
using OMS.API.Domain.Auth.Token;
using OMS.API.Domain.Category.Services;
using OMS.API.Domain.Customer.Services;
using OMS.API.Domain.ExchangeRate.Services;
using OMS.API.Domain.Order.Services;
using OMS.API.Domain.Product.Services;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Domain.Supplier.Services;
using OMS.API.Domain.User.Services;

namespace OMS.API.Domain.Product.Services;

public sealed class ProductService(IProductRepository productRepository) : IProductService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "updatedat",
        "sku",
        "name",
        "price",
        "stock",
        "isactive"
    };

    public async Task<PaginatedResult<ProductResponse>> ListAsync(ProductListRequest request, CancellationToken cancellationToken)
    {
        EnsureSupportedSortField(request.SortBy);

        var products = await productRepository.ListAsync(request, cancellationToken);

        return new PaginatedResult<ProductResponse>(
            products.Items.Select(MapProduct),
            products.Pagination);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("ProductEntity was not found.");

        return MapProduct(product);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var normalizedSku = ProductEntity.NormalizeSku(request.SKU);

        if (await productRepository.SkuExistsAsync(normalizedSku, excludingProductId: null, cancellationToken))
        {
            throw new ConflictException("ProductEntity SKU already exists.");
        }

        var category = await GetRequiredCategoryAsync(request.CategoryId, requireActive: true, cancellationToken);
        var supplier = await GetOptionalSupplierAsync(request.SupplierId, requireActive: true, cancellationToken);
        var product = new ProductEntity
        {
            SKU = normalizedSku,
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = category.Id,
            SupplierId = supplier?.Id,
            IsActive = true
        };

        await productRepository.AddAsync(product, cancellationToken);
        await SaveChangesHandlingConcurrencyAsync(cancellationToken);

        return MapProduct(product, category, supplier);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("ProductEntity was not found.");
        var normalizedSku = ProductEntity.NormalizeSku(request.SKU);

        if (await productRepository.SkuExistsAsync(normalizedSku, id, cancellationToken))
        {
            throw new ConflictException("ProductEntity SKU already exists.");
        }

        var category = await GetRequiredCategoryAsync(request.CategoryId, product.IsActive, cancellationToken);
        var supplier = await GetOptionalSupplierAsync(request.SupplierId, product.IsActive, cancellationToken);

        product.SKU = normalizedSku;
        product.Name = request.Name.Trim();
        product.Unit = request.Unit.Trim();
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = category.Id;
        product.SupplierId = supplier?.Id;

        await SaveChangesHandlingConcurrencyAsync(cancellationToken);

        return MapProduct(product, category, supplier);
    }

    public async Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("ProductEntity was not found.");
        var category = await GetRequiredCategoryAsync(product.CategoryId, request.IsActive, cancellationToken);
        var supplier = await GetOptionalSupplierAsync(product.SupplierId, request.IsActive, cancellationToken);

        product.IsActive = request.IsActive;

        await SaveChangesHandlingConcurrencyAsync(cancellationToken);

        return MapProduct(product, category, supplier);
    }

    private async Task<CategoryEntity> GetRequiredCategoryAsync(
        Guid categoryId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var category = await productRepository.GetCategoryByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("CategoryEntity was not found.");

        if (requireActive && !category.IsActive)
        {
            throw new ConflictException("Active products require an active category.");
        }

        return category;
    }

    private async Task<SupplierEntity?> GetOptionalSupplierAsync(
        Guid? supplierId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        if (!supplierId.HasValue)
        {
            return null;
        }

        var supplier = await productRepository.GetSupplierByIdAsync(supplierId.Value, cancellationToken)
            ?? throw new NotFoundException("SupplierEntity was not found.");

        if (requireActive && !supplier.IsActive)
        {
            throw new ConflictException("Active products require an active supplier.");
        }

        return supplier;
    }

    private async Task SaveChangesHandlingConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await productRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("ProductEntity was modified by another request. Reload and try again.");
        }
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported product sort field.");
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static ProductResponse MapProduct(ProductEntity product)
    {
        var category = product.Category
            ?? throw new InvalidOperationException("ProductEntity category was not loaded.");

        return MapProduct(product, category, product.Supplier);
    }

    private static ProductResponse MapProduct(ProductEntity product, CategoryEntity category, SupplierEntity? supplier)
    {
        return new ProductResponse(
            product.Id,
            product.SKU,
            product.Name,
            product.Unit,
            product.Price,
            product.Stock,
            new ProductRelatedResourceResponse(category.Id, category.Name),
            supplier is null
                ? null
                : new ProductRelatedResourceResponse(supplier.Id, supplier.Name),
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }
}
