using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
using OMS.API.Domain.Order.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
using OrderEntity = global::OMS.API.Models.Order;
using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
using OrderStatusHistoryEntity = global::OMS.API.Models.OrderStatusHistory;
using ProductEntity = global::OMS.API.Models.Product;
using RoleEntity = global::OMS.API.Models.Role;
using UserEntity = global::OMS.API.Models.User;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Order;

public sealed class OrderRepository(ApplicationDbContext dbContext) : IOrderRepository
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task<CustomerEntity?> GetCustomerForOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public Task<UserEntity?> GetCreatedByUserAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProductEntity>> GetProductsForOrderUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken);

        return products.ToDictionary(product => product.Id);
    }

    public async Task<PaginatedResult<OrderEntity>> ListAsync(
        Guid? createdByUserId,
        OrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = ApplySorting(
            ApplyFilters(CreateReadQuery(createdByUserId), request),
            request.SortBy,
            request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var orders = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<OrderEntity>(
            orders,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<OrderEntity?> GetByIdAsync(
        Guid id,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        return CreateReadQuery(createdByUserId)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public Task<OrderEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .Include(order => order.Customer)
            .Include(order => order.CreatedByUser)
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public Task<OrderEntity?> GetByIdForCancellationAsync(
        Guid id,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .Include(order => order.Customer)
            .Include(order => order.CreatedByUser)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .AsQueryable();

        if (createdByUserId.HasValue)
        {
            query = query.Where(order => order.CreatedByUserId == createdByUserId.Value);
        }

        return query.SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderStatusHistoryEntity>?> GetStatusHistoryAsync(
        Guid orderId,
        Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.StatusHistory)
            .ThenInclude(history => history.ChangedByUser)
            .ThenInclude(user => user!.Role)
            .AsQueryable();

        if (createdByUserId.HasValue)
        {
            query = query.Where(order => order.CreatedByUserId == createdByUserId.Value);
        }

        var order = await query.SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        return order?.StatusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .ThenBy(history => history.Id)
            .ToArray();
    }

    public async Task AddAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        await dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<OrderEntity> CreateReadQuery(Guid? createdByUserId)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.CreatedByUser)
            .Include(order => order.Items)
            .AsQueryable();

        if (createdByUserId.HasValue)
        {
            query = query.Where(order => order.CreatedByUserId == createdByUserId.Value);
        }

        return query;
    }

    private static IQueryable<OrderEntity> ApplyFilters(IQueryable<OrderEntity> query, OrderQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = Enum.Parse<OrderStatusEntity>(request.Status.Trim(), ignoreCase: true);

            query = query.Where(order => order.Status == status);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(order => order.CustomerId == request.CustomerId.Value);
        }

        if (request.DateFrom.HasValue)
        {
            var dateFromUtc = NormalizeUtc(request.DateFrom.Value);

            query = query.Where(order => order.CreatedAtUtc >= dateFromUtc);
        }

        if (request.DateTo.HasValue)
        {
            var dateToUtc = NormalizeUtc(request.DateTo.Value);

            query = query.Where(order => order.CreatedAtUtc <= dateToUtc);
        }

        return query;
    }

    private static IOrderedQueryable<OrderEntity> ApplySorting(
        IQueryable<OrderEntity> query,
        string? sortBy,
        SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "updatedat" => descending
                ? query.OrderByDescending(order => order.UpdatedAtUtc).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.UpdatedAtUtc).ThenBy(order => order.Id),
            "ordernumber" => descending
                ? query.OrderByDescending(order => order.OrderNumber).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.OrderNumber).ThenBy(order => order.Id),
            "status" => descending
                ? query.OrderByDescending(order => order.Status).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.Status).ThenBy(order => order.Id),
            "totalamount" => descending
                ? query.OrderByDescending(order => order.TotalAmount).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.TotalAmount).ThenBy(order => order.Id),
            _ => descending
                ? query.OrderByDescending(order => order.CreatedAtUtc).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.CreatedAtUtc).ThenBy(order => order.Id)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
