namespace OMS.API.Models;

public sealed class DailySalesReportItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DailySalesReportId { get; set; }

    public DailySalesReport? DailySalesReport { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public string ProductSku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int QuantitySold { get; set; }

    public decimal Revenue { get; set; }

    public void NormalizeForStorage()
    {
        ProductSku = Product.NormalizeSku(ProductSku);
        ProductName = ProductName.Trim();
    }
}
