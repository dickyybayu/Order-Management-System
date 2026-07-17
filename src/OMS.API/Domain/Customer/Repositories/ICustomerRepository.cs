using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Customer.Dtos;
using CustomerEntity = global::OMS.API.Models.Customer;
namespace OMS.API.Domain.Customer.Repositories;

public interface ICustomerRepository
{
    Task<PaginatedResult<CustomerEntity>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken);

    Task<CustomerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingCustomerId, CancellationToken cancellationToken);

    Task AddAsync(CustomerEntity customer, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
