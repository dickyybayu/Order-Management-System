using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Supplier.Dtos;
using SupplierEntity = global::OMS.API.Models.Supplier;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;
using OMS.API.Domain.Auth.Services;
using OMS.API.Domain.Auth.Token;
using OMS.API.Domain.Category.Services;
using OMS.API.Domain.Customer.Services;
using OMS.API.Domain.ExchangeRate.Services;
using OMS.API.Domain.Order.Services;
using OMS.API.Domain.Product.Services;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Domain.Supplier.Services;
using OMS.API.Domain.User.Services;

namespace OMS.API.Domain.Supplier.Services;

public sealed class SupplierService(ISupplierRepository supplierRepository) : ISupplierService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "updatedat",
        "name",
        "email",
        "phone",
        "isactive"
    };

    public async Task<PaginatedResult<SupplierResponse>> ListAsync(
        SupplierListRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSupportedSortField(request.SortBy);

        var suppliers = await supplierRepository.ListAsync(request, cancellationToken);

        return new PaginatedResult<SupplierResponse>(
            suppliers.Items.Select(MapSupplier),
            suppliers.Pagination);
    }

    public async Task<SupplierResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await GetExistingSupplierAsync(id, cancellationToken);

        return MapSupplier(supplier);
    }

    public async Task<SupplierResponse> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = new SupplierEntity
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            IsActive = true
        };
        supplier.TrimStringFieldsForStorage();

        await supplierRepository.AddAsync(supplier, cancellationToken);
        await supplierRepository.SaveChangesAsync(cancellationToken);

        return MapSupplier(supplier);
    }

    public async Task<SupplierResponse> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await GetExistingSupplierForUpdateAsync(id, cancellationToken);

        supplier.Name = request.Name;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.Address = request.Address;
        supplier.TrimStringFieldsForStorage();

        await supplierRepository.SaveChangesAsync(cancellationToken);

        return MapSupplier(supplier);
    }

    public async Task<SupplierResponse> UpdateStatusAsync(
        Guid id,
        UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await GetExistingSupplierForUpdateAsync(id, cancellationToken);

        supplier.IsActive = request.IsActive;

        await supplierRepository.SaveChangesAsync(cancellationToken);

        return MapSupplier(supplier);
    }

    private async Task<SupplierEntity> GetExistingSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        return await supplierRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("SupplierEntity was not found.");
    }

    private async Task<SupplierEntity> GetExistingSupplierForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await supplierRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("SupplierEntity was not found.");
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported supplier sort field.");
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static SupplierResponse MapSupplier(SupplierEntity supplier)
    {
        return new SupplierResponse(
            supplier.Id,
            supplier.Name,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.IsActive,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc);
    }
}
