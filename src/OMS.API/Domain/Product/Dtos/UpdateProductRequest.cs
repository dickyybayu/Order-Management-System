using System.ComponentModel.DataAnnotations;
using OMS.API.Infrastructure.Shareds.Validators;

namespace OMS.API.Domain.Product.Dtos;

public sealed record UpdateProductRequest(
    [Required]
    [MaxLength(50)]
    string SKU,
    [Required]
    [MaxLength(150)]
    string Name,
    [Required]
    [MaxLength(30)]
    string Unit,
    [PositiveDecimal]
    decimal Price,
    [Range(0, int.MaxValue)]
    int Stock,
    Guid CategoryId,
    Guid? SupplierId);
