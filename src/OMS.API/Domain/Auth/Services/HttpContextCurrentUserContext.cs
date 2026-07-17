using System.Security.Claims;

namespace OMS.API.Domain.Auth.Services;

public sealed class HttpContextCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var parsedUserId)
                ? parsedUserId
                : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? FullName => User?.FindFirstValue(ClaimTypes.Name);

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);

    public Guid GetRequiredUserId()
    {
        return UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");
    }
}
