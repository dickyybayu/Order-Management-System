using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Supplier.Dtos;

public sealed record UpdateSupplierRequest(
    [Required]
    [MaxLength(150)]
    string Name,
    [EmailAddress]
    [MaxLength(255)]
    string? Email,
    [MaxLength(30)]
    string? Phone,
    [MaxLength(500)]
    string? Address);
