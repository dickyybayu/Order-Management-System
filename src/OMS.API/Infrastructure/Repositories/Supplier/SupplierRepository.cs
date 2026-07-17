using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
using OMS.API.Domain.Supplier.Dtos;
using SupplierEntity = global::OMS.API.Models.Supplier;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Supplier;

public sealed class SupplierRepository(ApplicationDbContext dbContext) : ISupplierRepository
{
    public async Task<PaginatedResult<SupplierEntity>> ListAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Suppliers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(supplier =>
                supplier.Name.Contains(search) ||
                (supplier.Email != null && supplier.Email.Contains(search)) ||
                (supplier.Phone != null && supplier.Phone.Contains(search)));
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var suppliers = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<SupplierEntity>(
            suppliers,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<SupplierEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public Task<SupplierEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public async Task AddAsync(SupplierEntity supplier, CancellationToken cancellationToken)
    {
        await dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<SupplierEntity> ApplySorting(
        IQueryable<SupplierEntity> query,
        string? sortBy,
        SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "name" => descending
                ? query.OrderByDescending(supplier => supplier.Name)
                : query.OrderBy(supplier => supplier.Name),
            "email" => descending
                ? query.OrderByDescending(supplier => supplier.Email)
                : query.OrderBy(supplier => supplier.Email),
            "phone" => descending
                ? query.OrderByDescending(supplier => supplier.Phone)
                : query.OrderBy(supplier => supplier.Phone),
            "isactive" => descending
                ? query.OrderByDescending(supplier => supplier.IsActive)
                : query.OrderBy(supplier => supplier.IsActive),
            "updatedat" => descending
                ? query.OrderByDescending(supplier => supplier.UpdatedAtUtc)
                : query.OrderBy(supplier => supplier.UpdatedAtUtc),
            _ => descending
                ? query.OrderByDescending(supplier => supplier.CreatedAtUtc)
                : query.OrderBy(supplier => supplier.CreatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
