using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Customer.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
using UserEntity = global::OMS.API.Models.User;
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

namespace OMS.API.Domain.Customer.Services;

public sealed class CustomerService(ICustomerRepository customerRepository) : ICustomerService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "updatedat",
        "name",
        "email",
        "isactive"
    };

    public async Task<PaginatedResult<CustomerResponse>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken)
    {
        EnsureSupportedSortField(request.SortBy);

        var customers = await customerRepository.ListAsync(request, cancellationToken);

        return new PaginatedResult<CustomerResponse>(
            customers.Items.Select(MapCustomer),
            customers.Pagination);
    }

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("CustomerEntity was not found.");

        return MapCustomer(customer);
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);

        if (await customerRepository.EmailExistsAsync(normalizedEmail, excludingCustomerId: null, cancellationToken))
        {
            throw new ConflictException("CustomerEntity email already exists.");
        }

        var customer = new CustomerEntity
        {
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            ShippingAddress = request.ShippingAddress,
            IsActive = true
        };
        customer.NormalizeForStorage();

        await customerRepository.AddAsync(customer, cancellationToken);
        await customerRepository.SaveChangesAsync(cancellationToken);

        return MapCustomer(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("CustomerEntity was not found.");
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);

        if (await customerRepository.EmailExistsAsync(normalizedEmail, id, cancellationToken))
        {
            throw new ConflictException("CustomerEntity email already exists.");
        }

        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.ShippingAddress = request.ShippingAddress;
        customer.NormalizeForStorage();

        await customerRepository.SaveChangesAsync(cancellationToken);

        return MapCustomer(customer);
    }

    public async Task<CustomerResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("CustomerEntity was not found.");

        customer.IsActive = request.IsActive;

        await customerRepository.SaveChangesAsync(cancellationToken);

        return MapCustomer(customer);
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported customer sort field.");
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static CustomerResponse MapCustomer(CustomerEntity customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.ShippingAddress,
            customer.IsActive,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc);
    }
}
