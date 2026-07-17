using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.User.Dtos;

namespace OMS.API.Domain.User.Services;

public interface IUserService
{
    Task<PaginatedResult<UserResponse>> ListAsync(UserListRequest request, CancellationToken cancellationToken);

    Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken);

    Task<UserResponse> UpdateRoleAsync(
        Guid id,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken);
}
