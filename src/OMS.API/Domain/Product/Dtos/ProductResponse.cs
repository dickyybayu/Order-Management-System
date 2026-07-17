namespace OMS.API.Domain.Product.Dtos;

public sealed record ProductResponse(
    Guid Id,
    string SKU,
    string Name,
    string Unit,
    decimal Price,
    int Stock,
    ProductRelatedResourceResponse Category,
    ProductRelatedResourceResponse? Supplier,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
