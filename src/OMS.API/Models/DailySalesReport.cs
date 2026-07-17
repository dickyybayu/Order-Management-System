namespace OMS.API.Models;

public sealed class DailySalesReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly ReportDate { get; set; }

    public int TotalOrders { get; set; }

    public decimal TotalRevenue { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public ICollection<DailySalesReportItem> Items { get; } = [];
}
