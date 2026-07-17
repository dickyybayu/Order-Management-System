using Microsoft.EntityFrameworkCore;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Infrastructure.Databases;
using OMS.API.Domain.User.Dtos;
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

namespace OMS.API.Infrastructure.Repositories.User;

public sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public async Task<PaginatedResult<UserEntity>> ListAsync(
        UserListRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalizedSearch = UserEntity.NormalizeEmail(request.Search);
            var trimmedSearch = request.Search.Trim();

            query = query.Where(user =>
                user.Email.Contains(normalizedSearch) ||
                user.FullName.Contains(trimmedSearch));
        }

        query = ApplySorting(query, request.SortBy, request.SortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<UserEntity>(
            users,
            new PaginationMetadata(request.Page, request.PageSize, totalItems));
    }

    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<UserEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingUserId,
        CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Email == normalizedEmail &&
                    (!excludingUserId.HasValue || user.Id != excludingUserId.Value),
                cancellationToken);
    }

    public Task<RoleEntity?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken)
    {
        return dbContext.Roles
            .SingleOrDefaultAsync(role => role.Name == roleName, cancellationToken);
    }

    public async Task AddAsync(UserEntity user, CancellationToken cancellationToken)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<UserEntity> ApplySorting(
        IQueryable<UserEntity> query,
        string? sortBy,
        SortDirection sortDirection)
    {
        var descending = sortDirection == SortDirection.Desc;

        return NormalizeSortBy(sortBy) switch
        {
            "email" => descending
                ? query.OrderByDescending(user => user.Email)
                : query.OrderBy(user => user.Email),
            "fullname" => descending
                ? query.OrderByDescending(user => user.FullName)
                : query.OrderBy(user => user.FullName),
            "role" => descending
                ? query.OrderByDescending(user => user.Role!.Name)
                : query.OrderBy(user => user.Role!.Name),
            "isactive" => descending
                ? query.OrderByDescending(user => user.IsActive)
                : query.OrderBy(user => user.IsActive),
            _ => descending
                ? query.OrderByDescending(user => user.CreatedAtUtc)
                : query.OrderBy(user => user.CreatedAtUtc)
        };
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
