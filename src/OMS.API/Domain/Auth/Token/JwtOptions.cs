using System.Text;

namespace OMS.API.Domain.Auth.Token;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Issuer { get; init; }

    public string? Audience { get; init; }

    public string? SigningKey { get; init; }

    public int ExpirationMinutes { get; init; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT audience is not configured.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        }

        if (ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("JWT expiration must be greater than zero minutes.");
        }
    }
}
