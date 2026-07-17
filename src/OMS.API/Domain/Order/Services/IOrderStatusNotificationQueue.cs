using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
namespace OMS.API.Domain.Order.Services;

public interface IOrderStatusNotificationQueue
{
    Task EnqueueStatusChangedAsync(
        Guid orderId,
        string orderNumber,
        OrderStatusEntity fromStatus,
        OrderStatusEntity toStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken);
}
