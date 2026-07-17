using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Domain.Reporting.Dtos;
using OMS.API.Domain.Reporting.Services;
using OMS.API.Infrastructure.Shareds.Models;

namespace OMS.API.Http.API.Version1.Reporting.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ReportingRead)]
[Route("reports")]
public sealed class ReportsController(IReportingService reportingService) : ControllerBase
{
    [HttpGet("daily-sales")]
    [ProducesResponseType(typeof(ApiResponse<DailySalesReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DailySalesReportResponse>>> GetDailySales(
        [FromQuery]
        [Required]
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var response = await reportingService.GetDailySalesReportAsync(date!.Value, cancellationToken);

        return Ok(ApiResponse<DailySalesReportResponse>.Ok(
            response,
            "Daily sales report retrieved successfully."));
    }
}
