using OrderStatusEntity = global::OMS.API.Models.OrderStatus;
using RoleEntity = global::OMS.API.Models.Role;
namespace OMS.API.Domain.Order.Dtos;

public sealed record OrderStatusHistoryResponse(
    Guid Id,
    OrderStatusEntity? FromStatus,
    OrderStatusEntity ToStatus,
    string? Note,
    DateTime ChangedAtUtc,
    OrderHistoryActorResponse ChangedBy);

public sealed record OrderHistoryActorResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? Role);
