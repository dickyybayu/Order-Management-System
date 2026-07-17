namespace OMS.API.Models;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public Guid CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public string? TrackingNumber { get; set; }

    public string CurrencyCode { get; set; } = "IDR";

    public decimal? ExchangeRate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; } = [];

    public ICollection<OrderStatusHistory> StatusHistory { get; } = [];

    public void NormalizeForStorage()
    {
        OrderNumber = OrderNumber.Trim();
        TrackingNumber = string.IsNullOrWhiteSpace(TrackingNumber) ? null : TrackingNumber.Trim();
        CurrencyCode = CurrencyCode.Trim().ToUpperInvariant();
    }
}
