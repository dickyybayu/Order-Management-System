using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS.API.Constants.Permission;
using OMS.API.Infrastructure.Shareds.Models;
using OMS.API.Infrastructure.Shareds.Pagination;
using OMS.API.Domain.Order.Dtos;
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

namespace OMS.API.Http.API.Version1.Order.Controllers;

[ApiController]
[Authorize]
[Route("orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<OrderResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<OrderResponse>>>> List(
        [FromQuery] OrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        var response = await orderService.ListAsync(request, cancellationToken);

        return Ok(ApiResponse<PaginatedResult<OrderResponse>>.Ok(response, "Orders retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await orderService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<OrderResponse>.Ok(response, "Order retrieved successfully."));
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<OrderStatusHistoryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OrderStatusHistoryResponse>>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await orderService.GetStatusHistoryAsync(id, cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<OrderStatusHistoryResponse>>.Ok(
            response,
            "Order history retrieved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.OrderApprove)]
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await orderService.ApproveAsync(id, cancellationToken);

        return Ok(ApiResponse<OrderResponse>.Ok(response, "Order approved successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.OrderShip)]
    [HttpPost("{id:guid}/ship")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Ship(
        Guid id,
        ShipOrderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await orderService.ShipAsync(id, request, cancellationToken);

        return Ok(ApiResponse<OrderResponse>.Ok(response, "Order shipped successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.OrderDeliver)]
    [HttpPost("{id:guid}/deliver")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Deliver(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await orderService.DeliverAsync(id, cancellationToken);

        return Ok(ApiResponse<OrderResponse>.Ok(response, "Order delivered successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.OrderCancel)]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Cancel(
        Guid id,
        CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await orderService.CancelAsync(id, request, cancellationToken);

        return Ok(ApiResponse<OrderResponse>.Ok(response, "Order cancelled successfully."));
    }

    [Authorize(Policy = AuthorizationPolicies.OrderCreate)]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<OrderResponse>>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await orderService.CreateAsync(request, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<OrderResponse>.Ok(response, "Order created successfully."));
    }
}
