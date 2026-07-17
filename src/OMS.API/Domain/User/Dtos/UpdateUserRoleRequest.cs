using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.User.Dtos;

public sealed record UpdateUserRoleRequest(
    [Required]
    [MaxLength(50)]
    string Role);
