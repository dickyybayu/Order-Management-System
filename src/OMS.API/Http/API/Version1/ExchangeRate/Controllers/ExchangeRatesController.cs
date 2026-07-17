using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Domain.ExchangeRate.Dtos;
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

namespace OMS.API.Http.API.Version1.ExchangeRate.Controllers;

[ApiController]
[Authorize]
[Route("exchange-rates")]
public sealed class ExchangeRatesController(ICurrencyService currencyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<ExchangeRateResponse>>> Get(
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken)
    {
        var response = await currencyService.GetExchangeRateAsync(from, to, cancellationToken);

        return Ok(ApiResponse<ExchangeRateResponse>.Ok(response, "Exchange rate retrieved successfully."));
    }
}
