using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Product.Dtos;
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

namespace OMS.API.Http.API.Version1.Product.Controllers;

[ApiController]
[Authorize]
[Route("products")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<ProductResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ProductResponse>>>> List(
        [FromQuery] ProductListRequest request,
        CancellationToken cancellationToken)
    {
        var response = await productService.ListAsync(request, cancellationToken);

        return Ok(ApiResponse<PaginatedResult<ProductResponse>>.Ok(response, "Products retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await productService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<ProductResponse>.Ok(response, "Product retrieved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var response = await productService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<ProductResponse>.Ok(response, "Product created successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var response = await productService.UpdateAsync(id, request, cancellationToken);

        return Ok(ApiResponse<ProductResponse>.Ok(response, "Product updated successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProductResponse>>> UpdateStatus(
        Guid id,
        UpdateProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await productService.UpdateStatusAsync(id, request, cancellationToken);

        return Ok(ApiResponse<ProductResponse>.Ok(response, "Product status updated successfully."));
    }
}
