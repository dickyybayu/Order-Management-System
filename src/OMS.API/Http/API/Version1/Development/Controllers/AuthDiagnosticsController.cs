using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Domain.Auth.Services;
using OMS.API.Infrastructure.Shareds.Models;

namespace OMS.API.Http.API.Version1.Development.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("auth-diagnostics")]
public sealed class AuthDiagnosticsController(ICurrentUserContext currentUser) : ControllerBase
{
    [Authorize]
    [HttpGet("protected")]
    public ActionResult<ApiResponse<object>> Protected()
    {
        return Ok(ApiResponse<object>.Ok(
            new
            {
                currentUser.IsAuthenticated,
                currentUser.UserId,
                currentUser.Email,
                currentUser.FullName,
                currentUser.Role
            },
            "Authenticated request accepted."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("admin")]
    public ActionResult<ApiResponse<string>> AdminOnly()
    {
        return Ok(ApiResponse<string>.Ok("admin", "Admin request accepted."));
    }
}
