using System.ComponentModel.DataAnnotations;
using OMS.API.Infrastructure.Shareds.Validators;

namespace OMS.API.Domain.Product.Dtos;

public sealed class CreateProductRequest
{
    public CreateProductRequest(
        string sku,
        string name,
        string unit,
        decimal price,
        int stock,
        Guid categoryId,
        Guid? supplierId)
    {
        SKU = sku;
        Name = name;
        Unit = unit;
        Price = price;
        Stock = stock;
        CategoryId = categoryId;
        SupplierId = supplierId;
    }

    [Required]
    [MaxLength(50)]
    public string SKU { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; }

    [Required]
    [MaxLength(30)]
    public string Unit { get; init; }

    [PositiveDecimal]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int Stock { get; init; }

    public Guid CategoryId { get; init; }

    public Guid? SupplierId { get; init; }
}
