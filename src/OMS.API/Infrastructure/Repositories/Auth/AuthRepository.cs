using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Databases;
using RoleEntity = global::OMS.API.Models.Role;
using UserEntity = global::OMS.API.Models.User;
using OMS.API.Domain.Auth.Repositories;
using OMS.API.Domain.Category.Repositories;
using OMS.API.Domain.Customer.Repositories;
using OMS.API.Domain.Order.Repositories;
using OMS.API.Domain.Product.Repositories;
using OMS.API.Domain.Reporting.Repositories;
using OMS.API.Domain.Supplier.Repositories;
using OMS.API.Domain.User.Repositories;

namespace OMS.API.Infrastructure.Repositories.Auth;

public sealed class AuthRepository(ApplicationDbContext dbContext) : IAuthRepository
{
    public Task<UserEntity?> GetUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    public Task<bool> UserExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    public Task<RoleEntity?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken)
    {
        return dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Name == roleName, cancellationToken);
    }

    public async Task AddUserAsync(UserEntity user, CancellationToken cancellationToken)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
