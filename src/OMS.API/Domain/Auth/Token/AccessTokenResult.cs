namespace OMS.API.Domain.Auth.Token;

public sealed record AccessTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);
