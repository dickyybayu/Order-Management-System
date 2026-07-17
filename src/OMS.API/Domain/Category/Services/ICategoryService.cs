using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Category.Dtos;

namespace OMS.API.Domain.Category.Services;

public interface ICategoryService
{
    Task<PaginatedResult<CategoryResponse>> ListAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken);

    Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateStatusAsync(
        Guid id,
        UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken);
}
