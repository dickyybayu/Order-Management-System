using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
namespace OMS.API.Infrastructure.Queues;

public sealed record OrderStatusNotificationPayload(
    Guid OrderId,
    string OrderNumber,
    OrderStatusEntity PreviousStatus,
    OrderStatusEntity NewStatus,
    Guid ChangedByUserId,
    DateTime QueuedAtUtc);
