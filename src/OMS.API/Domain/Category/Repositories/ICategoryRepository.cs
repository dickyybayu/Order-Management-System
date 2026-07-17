using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Category.Dtos;
using CategoryEntity = global::OMS.API.Models.Category;
namespace OMS.API.Domain.Category.Repositories;

public interface ICategoryRepository
{
    Task<PaginatedResult<CategoryEntity>> ListAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken);

    Task<CategoryEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? excludingCategoryId,
        CancellationToken cancellationToken);

    Task AddAsync(CategoryEntity category, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
