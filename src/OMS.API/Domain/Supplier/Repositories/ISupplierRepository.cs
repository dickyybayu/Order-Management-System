using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Supplier.Dtos;
using SupplierEntity = global::OMS.API.Models.Supplier;
namespace OMS.API.Domain.Supplier.Repositories;

public interface ISupplierRepository
{
    Task<PaginatedResult<SupplierEntity>> ListAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken);

    Task<SupplierEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SupplierEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(SupplierEntity supplier, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
