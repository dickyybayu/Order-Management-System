using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Category.Dtos;
using CategoryEntity = global::OMS.API.Models.Category;
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

namespace OMS.API.Domain.Category.Services;

public sealed class CategoryService(ICategoryRepository categoryRepository) : ICategoryService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "updatedat",
        "name",
        "isactive"
    };

    public async Task<PaginatedResult<CategoryResponse>> ListAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSupportedSortField(request.SortBy);

        var categories = await categoryRepository.ListAsync(request, cancellationToken);

        return new PaginatedResult<CategoryResponse>(
            categories.Items.Select(MapCategory),
            categories.Pagination);
    }

    public async Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetExistingCategoryAsync(id, cancellationToken);

        return MapCategory(category);
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = CategoryEntity.NormalizeName(request.Name);

        if (await categoryRepository.NameExistsAsync(
            normalizedName,
            excludingCategoryId: null,
            cancellationToken))
        {
            throw new ConflictException("CategoryEntity name already exists.");
        }

        var category = new CategoryEntity
        {
            Name = normalizedName,
            Description = NormalizeDescription(request.Description),
            IsActive = true
        };

        await categoryRepository.AddAsync(category, cancellationToken);
        await categoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetExistingCategoryForUpdateAsync(id, cancellationToken);
        var normalizedName = CategoryEntity.NormalizeName(request.Name);

        if (await categoryRepository.NameExistsAsync(normalizedName, id, cancellationToken))
        {
            throw new ConflictException("CategoryEntity name already exists.");
        }

        category.Name = normalizedName;
        category.Description = NormalizeDescription(request.Description);

        await categoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    public async Task<CategoryResponse> UpdateStatusAsync(
        Guid id,
        UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetExistingCategoryForUpdateAsync(id, cancellationToken);

        category.IsActive = request.IsActive;

        await categoryRepository.SaveChangesAsync(cancellationToken);

        return MapCategory(category);
    }

    private async Task<CategoryEntity> GetExistingCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        return await categoryRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("CategoryEntity was not found.");
    }

    private async Task<CategoryEntity> GetExistingCategoryForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await categoryRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("CategoryEntity was not found.");
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported category sort field.");
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
    }

    private static CategoryResponse MapCategory(CategoryEntity category)
    {
        return new CategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);
    }
}
