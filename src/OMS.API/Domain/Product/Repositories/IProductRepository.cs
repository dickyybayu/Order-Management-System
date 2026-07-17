using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Product.Dtos;
using CategoryEntity = global::OMS.API.Models.Category;
using ProductEntity = global::OMS.API.Models.Product;
using SupplierEntity = global::OMS.API.Models.Supplier;
namespace OMS.API.Domain.Product.Repositories;

public interface IProductRepository
{
    Task<PaginatedResult<ProductEntity>> ListAsync(ProductListRequest request, CancellationToken cancellationToken);

    Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string normalizedSku, Guid? excludingProductId, CancellationToken cancellationToken);

    Task<CategoryEntity?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SupplierEntity?> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(ProductEntity product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
