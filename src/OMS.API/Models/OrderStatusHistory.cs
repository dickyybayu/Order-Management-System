namespace OMS.API.Models;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Order? Order { get; set; }

    public OrderStatus? FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    public Guid ChangedByUserId { get; set; }

    public User? ChangedByUser { get; set; }

    public string? Note { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public void NormalizeForStorage()
    {
        Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
    }
}
