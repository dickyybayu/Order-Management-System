using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Supplier.Dtos;

namespace OMS.API.Domain.Supplier.Services;

public interface ISupplierService
{
    Task<PaginatedResult<SupplierResponse>> ListAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken);

    Task<SupplierResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SupplierResponse> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken);

    Task<SupplierResponse> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken);

    Task<SupplierResponse> UpdateStatusAsync(
        Guid id,
        UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken);
}
