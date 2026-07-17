namespace OMS.API.Domain.Auth.Dtos;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AuthUserResponse User);
