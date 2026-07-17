using CustomerEntity = global::OMS.API.Models.Customer;
using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
namespace OMS.API.Domain.Order.Dtos;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    OrderRelatedResourceResponse Customer,
    OrderRelatedResourceResponse CreatedBy,
    OrderStatusEntity Status,
    string? TrackingNumber,
    string CurrencyCode,
    decimal? ExchangeRate,
    decimal Subtotal,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CancelledAtUtc,
    IReadOnlyCollection<OrderItemResponse> Items);

public sealed record OrderRelatedResourceResponse(
    Guid Id,
    string Name);

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
