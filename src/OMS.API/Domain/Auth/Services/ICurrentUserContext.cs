namespace OMS.API.Domain.Auth.Services;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    string? FullName { get; }

    string? Role { get; }

    Guid GetRequiredUserId();
}
