using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Product.Dtos;

namespace OMS.API.Domain.Product.Services;

public interface IProductService
{
    Task<PaginatedResult<ProductResponse>> ListAsync(ProductListRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken);
}
