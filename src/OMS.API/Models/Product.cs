namespace OMS.API.Models;

public sealed class Product : AuditableEntity
{
    public string SKU { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public void NormalizeForStorage()
    {
        SKU = NormalizeSku(SKU);
        Name = Name.Trim();
        Unit = Unit.Trim();
    }

    public static string NormalizeSku(string sku)
    {
        return sku.Trim().ToUpperInvariant();
    }
}
