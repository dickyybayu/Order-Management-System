using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Exceptions;
using OMS.API.Domain.Auth.Dtos;
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

namespace OMS.API.Domain.Auth.Services;

public sealed class AuthService(
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<AuthUserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);

        if (await authRepository.UserExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("Email is already registered.");
        }

        var role = await authRepository.GetRoleByNameAsync(
            SystemRoleNames.SalesOperator,
            cancellationToken)
            ?? throw new BusinessRuleException("Sales operator role is not configured.");

        var user = new UserEntity
        {
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.Password),
            RoleId = role.Id,
            IsActive = true
        };

        await authRepository.AddUserAsync(user, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user, role.Name);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = UserEntity.NormalizeEmail(request.Email);
        var user = await authRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null ||
            !user.IsActive ||
            user.Role is null ||
            !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var token = tokenService.CreateAccessToken(user, user.Role.Name);

        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            MapUser(user, user.Role.Name));
    }

    private static AuthUserResponse MapUser(UserEntity user, string roleName)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            roleName);
    }
}
