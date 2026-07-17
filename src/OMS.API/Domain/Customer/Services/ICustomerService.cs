using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Customer.Dtos;

namespace OMS.API.Domain.Customer.Services;

public interface ICustomerService
{
    Task<PaginatedResult<CustomerResponse>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken);
}
