using RoleEntity = global::OMS.API.Models.Role;
using UserEntity = global::OMS.API.Models.User;
namespace OMS.API.Domain.Auth.Repositories;

public interface IAuthRepository
{
    Task<UserEntity?> GetUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> UserExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<RoleEntity?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken);

    Task AddUserAsync(UserEntity user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
