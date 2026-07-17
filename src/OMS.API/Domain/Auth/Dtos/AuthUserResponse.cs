namespace OMS.API.Domain.Auth.Dtos;

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role);
