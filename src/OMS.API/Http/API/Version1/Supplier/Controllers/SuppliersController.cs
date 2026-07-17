using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Supplier.Dtos;
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

namespace OMS.API.Http.API.Version1.Supplier.Controllers;

[ApiController]
[Authorize]
[Route("suppliers")]
public sealed class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<SupplierResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<SupplierResponse>>>> List(
        [FromQuery] SupplierListRequest request,
        CancellationToken cancellationToken)
    {
        var response = await supplierService.ListAsync(request, cancellationToken);

        return Ok(ApiResponse<PaginatedResult<SupplierResponse>>.Ok(
            response,
            "Suppliers retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await supplierService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<SupplierResponse>.Ok(response, "Supplier retrieved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SupplierResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> Create(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var response = await supplierService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<SupplierResponse>.Ok(response, "Supplier created successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> Update(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var response = await supplierService.UpdateAsync(id, request, cancellationToken);

        return Ok(ApiResponse<SupplierResponse>.Ok(response, "Supplier updated successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<SupplierResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SupplierResponse>>> UpdateStatus(
        Guid id,
        UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await supplierService.UpdateStatusAsync(id, request, cancellationToken);

        return Ok(ApiResponse<SupplierResponse>.Ok(response, "Supplier status updated successfully."));
    }
}
