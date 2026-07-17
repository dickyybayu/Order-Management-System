using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Customer.Dtos;

public sealed record CreateCustomerRequest(
    [Required]
    [MaxLength(150)]
    string Name,
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    string Email,
    [MaxLength(30)]
    string? Phone,
    [Required]
    [MaxLength(500)]
    string ShippingAddress);
