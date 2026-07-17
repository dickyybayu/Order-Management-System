using OMS.API.Domain.Auth.Dtos;

namespace OMS.API.Domain.Auth.Services;

public interface IAuthService
{
    Task<AuthUserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
