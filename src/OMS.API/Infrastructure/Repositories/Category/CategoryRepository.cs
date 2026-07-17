using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
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

namespace OMS.API.Infrastructure.Repositories.Category;

public sealed class CategoryRepository(ApplicationDbContext dbContext) : ICategoryRepository
{
    public async Task<PaginatedResult<CategoryEntity>> ListAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Categories
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = CategoryEntity.NormalizeName(request.Search);

            query = query.Where(category => category.Name.Contains(search));
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var categories = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<CategoryEntity>(
            categories,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<CategoryEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public Task<CategoryEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? excludingCategoryId,
        CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.Name == normalizedName &&
                    (!excludingCategoryId.HasValue || category.Id != excludingCategoryId.Value),
                cancellationToken);
    }

    public async Task AddAsync(CategoryEntity category, CancellationToken cancellationToken)
    {
        await dbContext.Categories.AddAsync(category, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<CategoryEntity> ApplySorting(
        IQueryable<CategoryEntity> query,
        string? sortBy,
        SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "name" => descending
                ? query.OrderByDescending(category => category.Name)
                : query.OrderBy(category => category.Name),
            "isactive" => descending
                ? query.OrderByDescending(category => category.IsActive)
                : query.OrderBy(category => category.IsActive),
            "updatedat" => descending
                ? query.OrderByDescending(category => category.UpdatedAtUtc)
                : query.OrderBy(category => category.UpdatedAtUtc),
            _ => descending
                ? query.OrderByDescending(category => category.CreatedAtUtc)
                : query.OrderBy(category => category.CreatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
