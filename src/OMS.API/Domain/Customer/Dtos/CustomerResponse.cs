namespace OMS.API.Domain.Customer.Dtos;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string ShippingAddress,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
