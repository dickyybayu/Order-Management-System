using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Category.Dtos;
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

namespace OMS.API.Http.API.Version1.Category.Controllers;

[ApiController]
[Authorize]
[Route("categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<CategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CategoryResponse>>>> List(
        [FromQuery] CategoryListRequest request,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.ListAsync(request, cancellationToken);

        return Ok(ApiResponse<PaginatedResult<CategoryResponse>>.Ok(
            response,
            "Categories retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<CategoryResponse>.Ok(response, "Category retrieved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<CategoryResponse>.Ok(response, "Category created successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> Update(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.UpdateAsync(id, request, cancellationToken);

        return Ok(ApiResponse<CategoryResponse>.Ok(response, "Category updated successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CategoryResponse>>> UpdateStatus(
        Guid id,
        UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.UpdateStatusAsync(id, request, cancellationToken);

        return Ok(ApiResponse<CategoryResponse>.Ok(response, "Category status updated successfully."));
    }
}
