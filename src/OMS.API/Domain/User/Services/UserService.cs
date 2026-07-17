using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Exceptions;
using OMS.API.Infrastructure.Shareds.Pagination;
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

namespace OMS.API.Domain.User.Services;

public sealed class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ICurrentUserContext currentUser) : IUserService
{
    private static readonly ISet<string> AllowedSortFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "createdat",
        "email",
        "fullname",
        "role",
        "isactive"
    };

    public async Task<PaginatedResult<UserResponse>> ListAsync(
        UserListRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSupportedSortField(request.SortBy);

        var users = await userRepository.ListAsync(request, cancellationToken);

        return new PaginatedResult<UserResponse>(
            users.Items.Select(MapUser),
            users.Pagination);
    }

    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await GetExistingUserAsync(id, cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRoleName = NormalizeRoleName(request.Role);
        var role = await GetSupportedRoleAsync(normalizedRoleName, cancellationToken);
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);

        if (await userRepository.EmailExistsAsync(normalizedEmail, excludingUserId: null, cancellationToken))
        {
            throw new ConflictException("Email is already registered.");
        }

        var user = new UserEntity
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponse> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetExistingUserForUpdateAsync(id, cancellationToken);
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);

        if (await userRepository.EmailExistsAsync(normalizedEmail, id, cancellationToken))
        {
            throw new ConflictException("Email is already registered.");
        }

        user.Email = normalizedEmail;
        user.FullName = request.FullName.Trim();

        await userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponse> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetExistingUserForUpdateAsync(id, cancellationToken);

        if (IsSelf(user.Id) && !request.IsActive)
        {
            throw new ConflictException("Administrators cannot deactivate their own account.");
        }

        user.IsActive = request.IsActive;

        await userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponse> UpdateRoleAsync(
        Guid id,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRoleName = NormalizeRoleName(request.Role);
        var role = await GetSupportedRoleAsync(normalizedRoleName, cancellationToken);
        var user = await GetExistingUserForUpdateAsync(id, cancellationToken);

        if (IsSelf(user.Id) && normalizedRoleName != SystemRoleNames.Admin)
        {
            throw new ConflictException("Administrators cannot remove their own administrative access.");
        }

        user.RoleId = role.Id;
        user.Role = role;

        await userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    private async Task<UserEntity> GetExistingUserAsync(Guid id, CancellationToken cancellationToken)
    {
        return await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("UserEntity was not found.");
    }

    private async Task<UserEntity> GetExistingUserForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return await userRepository.GetByIdForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("UserEntity was not found.");
    }

    private async Task<RoleEntity> GetSupportedRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!SystemRoleNames.All.Contains(roleName, StringComparer.Ordinal))
        {
            throw new NotFoundException("RoleEntity was not found.");
        }

        return await userRepository.GetRoleByNameAsync(roleName, cancellationToken)
            ?? throw new NotFoundException("RoleEntity was not found.");
    }

    private bool IsSelf(Guid userId)
    {
        return currentUser.UserId == userId;
    }

    private static void EnsureSupportedSortField(string? sortBy)
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);

        if (!AllowedSortFields.Contains(normalizedSortBy))
        {
            throw new BusinessRuleException("Unsupported user sort field.");
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string NormalizeRoleName(string roleName)
    {
        return roleName.Trim().ToLowerInvariant();
    }

    private static UserResponse MapUser(UserEntity user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role?.Name ?? string.Empty,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }
}
