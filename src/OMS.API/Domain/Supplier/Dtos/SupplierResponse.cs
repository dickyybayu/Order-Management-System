namespace OMS.API.Domain.Supplier.Dtos;

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
