using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Order.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
using OrderEntity = global::OMS.API.Models.Order;
using OrderStatusHistoryEntity = global::OMS.API.Models.OrderStatusHistory;
using ProductEntity = global::OMS.API.Models.Product;
using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Domain.Order.Repositories;

public interface IOrderRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task<CustomerEntity?> GetCustomerForOrderAsync(Guid id, CancellationToken cancellationToken);

    Task<UserEntity?> GetCreatedByUserAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ProductEntity>> GetProductsForOrderUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task<PaginatedResult<OrderEntity>> ListAsync(
        Guid? createdByUserId,
        OrderQueryRequest request,
        CancellationToken cancellationToken);

    Task<OrderEntity?> GetByIdAsync(
        Guid id,
        Guid? createdByUserId,
        CancellationToken cancellationToken);

    Task<OrderEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderEntity?> GetByIdForCancellationAsync(
        Guid id,
        Guid? createdByUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrderStatusHistoryEntity>?> GetStatusHistoryAsync(
        Guid orderId,
        Guid? createdByUserId,
        CancellationToken cancellationToken);

    Task AddAsync(OrderEntity order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
