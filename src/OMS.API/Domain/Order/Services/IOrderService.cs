using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Order.Dtos;

namespace OMS.API.Domain.Order.Services;

public interface IOrderService
{
    Task<PaginatedResult<OrderResponse>> ListAsync(
        OrderQueryRequest request,
        CancellationToken cancellationToken);

    Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrderStatusHistoryResponse>> GetStatusHistoryAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OrderResponse> ApproveAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderResponse> ShipAsync(
        Guid id,
        ShipOrderRequest request,
        CancellationToken cancellationToken);

    Task<OrderResponse> DeliverAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderResponse> CancelAsync(
        Guid id,
        CancelOrderRequest request,
        CancellationToken cancellationToken);

    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}
