using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.User.Dtos;
using RoleEntity = global::OMS.API.Models.Role;
using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Domain.User.Repositories;

public interface IUserRepository
{
    Task<PaginatedResult<UserEntity>> ListAsync(UserListRequest request, CancellationToken cancellationToken);

    Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId, CancellationToken cancellationToken);

    Task<RoleEntity?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken);

    Task AddAsync(UserEntity user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
