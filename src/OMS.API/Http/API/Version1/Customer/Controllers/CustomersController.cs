using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Customer.Dtos;
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

namespace OMS.API.Http.API.Version1.Customer.Controllers;

[ApiController]
[Authorize]
[Route("customers")]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<CustomerResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CustomerResponse>>>> List(
        [FromQuery] CustomerListRequest request,
        CancellationToken cancellationToken)
    {
        var response = await customerService.ListAsync(request, cancellationToken);

        return Ok(ApiResponse<PaginatedResult<CustomerResponse>>.Ok(response, "Customers retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await customerService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<CustomerResponse>.Ok(response, "Customer retrieved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerWrite)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var response = await customerService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CustomerResponse>.Ok(response, "Customer created successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerWrite)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Update(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var response = await customerService.UpdateAsync(id, request, cancellationToken);

        return Ok(ApiResponse<CustomerResponse>.Ok(response, "Customer updated successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerWrite)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> UpdateStatus(
        Guid id,
        UpdateCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await customerService.UpdateStatusAsync(id, request, cancellationToken);

        return Ok(ApiResponse<CustomerResponse>.Ok(response, "Customer status updated successfully."));
    }
}
