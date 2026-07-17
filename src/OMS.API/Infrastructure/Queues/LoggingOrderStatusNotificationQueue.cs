using OrderEntity = global::OMS.API.Models.Order;
using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
using UserEntity = global::OMS.API.Models.User;
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

namespace OMS.API.Infrastructure.Queues;

public sealed class LoggingOrderStatusNotificationQueue(
    ILogger<LoggingOrderStatusNotificationQueue> logger) : IOrderStatusNotificationQueue
{
    public Task EnqueueStatusChangedAsync(
        Guid orderId,
        string orderNumber,
        OrderStatusEntity fromStatus,
        OrderStatusEntity toStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "OrderEntity status notification queued for order {OrderId} ({OrderNumber}) from {FromStatus} to {ToStatus} by user {UserId}",
            orderId,
            orderNumber,
            fromStatus,
            toStatus,
            changedByUserId);

        return Task.CompletedTask;
    }
}
