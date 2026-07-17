using Microsoft.EntityFrameworkCore;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Order.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
using OrderEntity = global::OMS.API.Models.Order;
using OrderItemEntity = global::OMS.API.Models.OrderItem;
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

namespace OMS.API.Domain.Order.Services;

public sealed class OrderService(
    IOrderRepository orderRepository,
    ICurrentUserContext currentUser,
    IOrderNumberGenerator orderNumberGenerator,
    IOrderStatusNotificationQueue notificationQueue,
    ICurrencyService currencyService,
    OrderCurrencyOptions currencyOptions,
    ILogger<OrderService> logger) : IOrderService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "updatedat",
        "ordernumber",
        "status",
        "totalamount"
    };

    public async Task<PaginatedResult<OrderResponse>> ListAsync(
        OrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureQueryRequestIsSupported(request);

        var scopedCreatedByUserId = GetScopedCreatedByUserId();
        var orders = await orderRepository.ListAsync(scopedCreatedByUserId, request, cancellationToken);

        return new PaginatedResult<OrderResponse>(
            orders.Items.Select(MapOrder),
            orders.Pagination);
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var scopedCreatedByUserId = GetScopedCreatedByUserId();
        var order = await orderRepository.GetByIdAsync(id, scopedCreatedByUserId, cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        return MapOrder(order);
    }

    public async Task<IReadOnlyCollection<OrderStatusHistoryResponse>> GetStatusHistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var scopedCreatedByUserId = GetScopedCreatedByUserId();
        var history = await orderRepository.GetStatusHistoryAsync(
                id,
                scopedCreatedByUserId,
                cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        return history.Select(MapHistory).ToArray();
    }

    public async Task<OrderResponse> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser.GetRequiredUserId();
        OrderResponse response;

        try
        {
            response = await orderRepository.ExecuteInTransactionAsync(
                operationCancellationToken => ApproveInTransactionAsync(
                    id,
                    currentUserId,
                    operationCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("OrderEntity could not be approved because it changed. Please retry.");
        }

        await TryQueueStatusNotificationAsync(
            response,
            OrderStatusEntity.Pending,
            OrderStatusEntity.Processing,
            currentUserId,
            cancellationToken);

        return response;
    }

    public async Task<OrderResponse> ShipAsync(
        Guid id,
        ShipOrderRequest request,
        CancellationToken cancellationToken)
    {
        EnsureShipRequestIsSupported(request);

        var currentUserId = currentUser.GetRequiredUserId();
        OrderResponse response;

        try
        {
            response = await orderRepository.ExecuteInTransactionAsync(
                operationCancellationToken => ShipInTransactionAsync(
                    id,
                    request,
                    currentUserId,
                    operationCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("OrderEntity could not be shipped because it changed. Please retry.");
        }

        await TryQueueStatusNotificationAsync(
            response,
            OrderStatusEntity.Processing,
            OrderStatusEntity.Shipped,
            currentUserId,
            cancellationToken);

        return response;
    }

    public async Task<OrderResponse> DeliverAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser.GetRequiredUserId();
        OrderResponse response;

        try
        {
            response = await orderRepository.ExecuteInTransactionAsync(
                operationCancellationToken => DeliverInTransactionAsync(
                    id,
                    currentUserId,
                    operationCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("OrderEntity could not be delivered because it changed. Please retry.");
        }

        await TryQueueStatusNotificationAsync(
            response,
            OrderStatusEntity.Shipped,
            OrderStatusEntity.Delivered,
            currentUserId,
            cancellationToken);

        return response;
    }

    public async Task<OrderResponse> CancelAsync(
        Guid id,
        CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCancelRequestIsSupported(request);

        var currentUserId = currentUser.GetRequiredUserId();
        var scopedCreatedByUserId = GetCancellationScopedCreatedByUserId();
        OrderStatusEntity previousStatus;
        OrderResponse response;

        try
        {
            (response, previousStatus) = await orderRepository.ExecuteInTransactionAsync(
                operationCancellationToken => CancelInTransactionAsync(
                    id,
                    request,
                    currentUserId,
                    scopedCreatedByUserId,
                    operationCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("OrderEntity could not be cancelled because it changed. Please retry.");
        }

        await TryQueueStatusNotificationAsync(
            response,
            previousStatus,
            OrderStatusEntity.Cancelled,
            currentUserId,
            cancellationToken);

        return response;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        EnsureCreateRequestIsSupported(request);

        var currentUserId = currentUser.GetRequiredUserId();
        var normalizedCurrencyCode = CurrencyCode.Normalize(request.CurrencyCode);
        var exchangeRate = await GetExchangeRateForOrderAsync(normalizedCurrencyCode, cancellationToken);

        try
        {
            return await orderRepository.ExecuteInTransactionAsync(
                operationCancellationToken => CreateInTransactionAsync(
                    request,
                    currentUserId,
                    normalizedCurrencyCode,
                    exchangeRate,
                    operationCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("OrderEntity could not be created because product stock changed. Please retry.");
        }
    }

    private async Task<OrderResponse> CreateInTransactionAsync(
        CreateOrderRequest request,
        Guid currentUserId,
        string currencyCode,
        decimal exchangeRate,
        CancellationToken cancellationToken)
    {
        var createdAtUtc = DateTime.UtcNow;
        var customer = await orderRepository.GetCustomerForOrderAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("CustomerEntity was not found.");

        if (!customer.IsActive)
        {
            throw new BusinessRuleException("Inactive customer cannot be used for a new order.");
        }

        var createdByUser = await orderRepository.GetCreatedByUserAsync(currentUserId, cancellationToken)
            ?? throw new NotFoundException("Authenticated user was not found.");

        var productIds = request.Items.Select(item => item.ProductId).ToArray();
        var products = await orderRepository.GetProductsForOrderUpdateAsync(productIds, cancellationToken);
        var order = new OrderEntity
        {
            OrderNumber = orderNumberGenerator.Create(createdAtUtc),
            CustomerId = customer.Id,
            CreatedByUserId = currentUserId,
            Status = OrderStatusEntity.Pending,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            CreatedAtUtc = createdAtUtc
        };

        foreach (var requestItem in request.Items)
        {
            if (!products.TryGetValue(requestItem.ProductId, out var product))
            {
                throw new NotFoundException("ProductEntity was not found.");
            }

            if (!product.IsActive)
            {
                throw new BusinessRuleException("Inactive product cannot be used for a new order.");
            }

            if (product.Stock < requestItem.Quantity)
            {
                throw new BusinessRuleException("Insufficient product stock.");
            }

            product.Stock -= requestItem.Quantity;

            var lineTotal = product.Price * requestItem.Quantity;
            order.Items.Add(new OrderItemEntity
            {
                ProductId = product.Id,
                ProductSku = product.SKU,
                ProductName = product.Name,
                Quantity = requestItem.Quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            });
            order.Subtotal += lineTotal;
        }

        order.TotalAmount = decimal.Round(order.Subtotal * exchangeRate, 2, MidpointRounding.AwayFromZero);
        order.StatusHistory.Add(new OrderStatusHistoryEntity
        {
            FromStatus = null,
            ToStatus = OrderStatusEntity.Pending,
            ChangedByUserId = currentUserId,
            ChangedAtUtc = createdAtUtc
        });

        await orderRepository.AddAsync(order, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order, customer, createdByUser);
    }

    private async Task<OrderResponse> ApproveInTransactionAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var changedAtUtc = DateTime.UtcNow;
        var order = await orderRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        if (order.Status != OrderStatusEntity.Pending)
        {
            throw new ConflictException("Only pending orders can be approved.");
        }

        order.Status = OrderStatusEntity.Processing;
        order.UpdatedAtUtc = changedAtUtc;
        order.StatusHistory.Add(new OrderStatusHistoryEntity
        {
            FromStatus = OrderStatusEntity.Pending,
            ToStatus = OrderStatusEntity.Processing,
            ChangedByUserId = currentUserId,
            ChangedAtUtc = changedAtUtc
        });

        await orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    private async Task<OrderResponse> ShipInTransactionAsync(
        Guid id,
        ShipOrderRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var changedAtUtc = DateTime.UtcNow;
        var order = await orderRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        if (order.Status != OrderStatusEntity.Processing)
        {
            throw new ConflictException("Only processing orders can be shipped.");
        }

        order.Status = OrderStatusEntity.Shipped;
        order.TrackingNumber = NormalizeTrackingNumber(request.TrackingNumber);
        order.UpdatedAtUtc = changedAtUtc;
        order.StatusHistory.Add(new OrderStatusHistoryEntity
        {
            FromStatus = OrderStatusEntity.Processing,
            ToStatus = OrderStatusEntity.Shipped,
            ChangedByUserId = currentUserId,
            ChangedAtUtc = changedAtUtc
        });

        await orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    private async Task<OrderResponse> DeliverInTransactionAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var changedAtUtc = DateTime.UtcNow;
        var order = await orderRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        if (order.Status != OrderStatusEntity.Shipped)
        {
            throw new ConflictException("Only shipped orders can be delivered.");
        }

        order.Status = OrderStatusEntity.Delivered;
        order.UpdatedAtUtc = changedAtUtc;
        order.StatusHistory.Add(new OrderStatusHistoryEntity
        {
            FromStatus = OrderStatusEntity.Shipped,
            ToStatus = OrderStatusEntity.Delivered,
            ChangedByUserId = currentUserId,
            ChangedAtUtc = changedAtUtc
        });

        await orderRepository.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    private async Task<(OrderResponse Response, OrderStatusEntity PreviousStatus)> CancelInTransactionAsync(
        Guid id,
        CancelOrderRequest request,
        Guid currentUserId,
        Guid? scopedCreatedByUserId,
        CancellationToken cancellationToken)
    {
        var changedAtUtc = DateTime.UtcNow;
        var order = await orderRepository.GetByIdForCancellationAsync(
                id,
                scopedCreatedByUserId,
                cancellationToken)
            ?? throw new NotFoundException("OrderEntity was not found.");

        if (order.Status is not (OrderStatusEntity.Pending or OrderStatusEntity.Processing))
        {
            throw new ConflictException("Only pending or processing orders can be cancelled.");
        }

        var previousStatus = order.Status;

        foreach (var item in order.Items)
        {
            var product = item.Product
                ?? throw new ConflictException("OrderEntity product could not be loaded for stock restoration.");

            product.Stock += item.Quantity;
        }

        order.Status = OrderStatusEntity.Cancelled;
        order.CancelledAtUtc = changedAtUtc;
        order.UpdatedAtUtc = changedAtUtc;
        order.StatusHistory.Add(new OrderStatusHistoryEntity
        {
            FromStatus = previousStatus,
            ToStatus = OrderStatusEntity.Cancelled,
            ChangedByUserId = currentUserId,
            Note = NormalizeCancellationReason(request.Reason),
            ChangedAtUtc = changedAtUtc
        });

        await orderRepository.SaveChangesAsync(cancellationToken);

        return (MapOrder(order), previousStatus);
    }

    private static void EnsureCreateRequestIsSupported(CreateOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            throw new BusinessRuleException("OrderEntity must contain at least one item.");
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            throw new BusinessRuleException("OrderEntity item quantity must be greater than zero.");
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty))
        {
            throw new BusinessRuleException("ProductId is required.");
        }

        var duplicateProductId = request.Items
            .GroupBy(item => item.ProductId)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateProductId.HasValue)
        {
            throw new BusinessRuleException("Duplicate product lines are not allowed.");
        }
    }

    private static void EnsureShipRequestIsSupported(ShipOrderRequest request)
    {
        var trackingNumber = NormalizeTrackingNumber(request.TrackingNumber);

        if (trackingNumber.Length == 0)
        {
            throw new BusinessRuleException("TrackingNumber is required.");
        }

        if (trackingNumber.Length > 100)
        {
            throw new BusinessRuleException("TrackingNumber must not exceed 100 characters.");
        }
    }

    private static void EnsureCancelRequestIsSupported(CancelOrderRequest request)
    {
        var reason = NormalizeCancellationReason(request.Reason);

        if (reason.Length == 0)
        {
            throw new BusinessRuleException("Cancellation reason is required.");
        }

        if (reason.Length > 500)
        {
            throw new BusinessRuleException("Cancellation reason must not exceed 500 characters.");
        }
    }

    private static void EnsureQueryRequestIsSupported(OrderQueryRequest request)
    {
        EnsureSupportedStatus(request.Status);
        EnsureSupportedSortField(request.SortBy);

        if (request.DateFrom.HasValue &&
            request.DateTo.HasValue &&
            NormalizeUtc(request.DateFrom.Value) > NormalizeUtc(request.DateTo.Value))
        {
            throw new BusinessRuleException("DateFrom must not be after DateTo.");
        }
    }

    private static void EnsureSupportedStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        if (!Enum.TryParse<OrderStatusEntity>(status.Trim(), ignoreCase: true, out _) ||
            int.TryParse(status.Trim(), out _))
        {
            throw new BusinessRuleException("Unsupported order status.");
        }
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported order sort field.");
        }
    }

    private static string NormalizeTrackingNumber(string? trackingNumber)
    {
        return trackingNumber?.Trim() ?? string.Empty;
    }

    private static string NormalizeCancellationReason(string? reason)
    {
        return reason?.Trim() ?? string.Empty;
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

    private async Task<decimal> GetExchangeRateForOrderAsync(
        string requestedCurrencyCode,
        CancellationToken cancellationToken)
    {
        var baseCurrencyCode = CurrencyCode.Normalize(currencyOptions.BaseCurrencyCode);

        if (baseCurrencyCode == requestedCurrencyCode)
        {
            return 1m;
        }

        var response = await currencyService.GetExchangeRateAsync(
            baseCurrencyCode,
            requestedCurrencyCode,
            cancellationToken);

        return response.Rate;
    }

    private Guid? GetScopedCreatedByUserId()
    {
        return currentUser.Role is SystemRoleNames.Admin or SystemRoleNames.Supervisor
            ? null
            : currentUser.GetRequiredUserId();
    }

    private Guid? GetCancellationScopedCreatedByUserId()
    {
        return string.Equals(currentUser.Role, SystemRoleNames.SalesOperator, StringComparison.Ordinal)
            ? currentUser.GetRequiredUserId()
            : null;
    }

    private static OrderResponse MapOrder(OrderEntity order)
    {
        var customer = order.Customer
            ?? throw new InvalidOperationException("OrderEntity customer must be loaded for read responses.");
        var createdByUser = order.CreatedByUser
            ?? throw new InvalidOperationException("OrderEntity created-by user must be loaded for read responses.");

        return MapOrder(order, customer, createdByUser);
    }

    private static OrderResponse MapOrder(OrderEntity order, CustomerEntity customer, UserEntity createdByUser)
    {
        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            new OrderRelatedResourceResponse(customer.Id, customer.Name),
            new OrderRelatedResourceResponse(createdByUser.Id, createdByUser.FullName),
            order.Status,
            order.TrackingNumber,
            order.CurrencyCode,
            order.ExchangeRate,
            order.Subtotal,
            order.TotalAmount,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.CancelledAtUtc,
            order.Items.Select(item => new OrderItemResponse(
                    item.ProductId,
                    item.ProductSku,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal))
                .ToArray());
    }

    private static OrderStatusHistoryResponse MapHistory(OrderStatusHistoryEntity history)
    {
        var changedByUser = history.ChangedByUser
            ?? throw new InvalidOperationException("History actor must be loaded for read responses.");

        return new OrderStatusHistoryResponse(
            history.Id,
            history.FromStatus,
            history.ToStatus,
            history.Note,
            history.ChangedAtUtc,
            new OrderHistoryActorResponse(
                changedByUser.Id,
                changedByUser.FullName,
                changedByUser.Email,
                changedByUser.Role?.Name));
    }

    private async Task TryQueueStatusNotificationAsync(
        OrderResponse order,
        OrderStatusEntity fromStatus,
        OrderStatusEntity toStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await notificationQueue.EnqueueStatusChangedAsync(
                order.Id,
                order.OrderNumber,
                fromStatus,
                toStatus,
                changedByUserId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "OrderEntity status notification queue failed after changing order {OrderId} from {FromStatus} to {ToStatus}",
                order.Id,
                fromStatus,
                toStatus);
        }
    }
}
