using Coravel.Invocable;

namespace OMS.API.Infrastructure.Queues;

public sealed class OrderStatusNotificationJob(
    ILogger<OrderStatusNotificationJob> logger) :
    IInvocable,
    IInvocableWithPayload<OrderStatusNotificationPayload>
{
    public OrderStatusNotificationPayload Payload { get; set; } = new(
        Guid.Empty,
        string.Empty,
        default,
        default,
        Guid.Empty,
        DateTime.MinValue);

    public Task Invoke()
    {
        logger.LogInformation(
            "Order status notification execution started for order {OrderId} ({OrderNumber}) from {PreviousStatus} to {NewStatus} by user {ChangedByUserId} queued at {QueuedAtUtc}",
            Payload.OrderId,
            Payload.OrderNumber,
            Payload.PreviousStatus,
            Payload.NewStatus,
            Payload.ChangedByUserId,
            Payload.QueuedAtUtc);

        try
        {
            logger.LogInformation(
                "Order status notification execution finished for order {OrderId} ({OrderNumber})",
                Payload.OrderId,
                Payload.OrderNumber);

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Order status notification execution failed for order {OrderId} ({OrderNumber})",
                Payload.OrderId,
                Payload.OrderNumber);
            throw;
        }
    }
}
