using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Category.Dtos;

public sealed record UpdateCategoryRequest(
    [Required]
    [MaxLength(100)]
    string Name,
    [MaxLength(500)]
    string? Description);
