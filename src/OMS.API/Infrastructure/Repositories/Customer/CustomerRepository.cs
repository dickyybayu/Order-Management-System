using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
using OMS.API.Domain.Customer.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
using UserEntity = global::OMS.API.Models.User;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Customer;

public sealed class CustomerRepository(ApplicationDbContext dbContext) : ICustomerRepository
{
    public async Task<PaginatedResult<CustomerEntity>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var normalizedEmailSearch = UserEntity.NormalizeEmail(search);

            query = query.Where(customer =>
                customer.Name.Contains(search) ||
                customer.Email.Contains(normalizedEmailSearch));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(customer => customer.IsActive == request.IsActive.Value);
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var customers = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<CustomerEntity>(
            customers,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<CustomerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public Task<CustomerEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingCustomerId, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer => customer.Email == normalizedEmail &&
                    (!excludingCustomerId.HasValue || customer.Id != excludingCustomerId.Value),
                cancellationToken);
    }

    public async Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<CustomerEntity> ApplySorting(IQueryable<CustomerEntity> query, string? sortBy, SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "name" => descending ? query.OrderByDescending(customer => customer.Name) : query.OrderBy(customer => customer.Name),
            "email" => descending ? query.OrderByDescending(customer => customer.Email) : query.OrderBy(customer => customer.Email),
            "isactive" => descending ? query.OrderByDescending(customer => customer.IsActive) : query.OrderBy(customer => customer.IsActive),
            "updatedat" => descending ? query.OrderByDescending(customer => customer.UpdatedAtUtc) : query.OrderBy(customer => customer.UpdatedAtUtc),
            _ => descending ? query.OrderByDescending(customer => customer.CreatedAtUtc) : query.OrderBy(customer => customer.CreatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
