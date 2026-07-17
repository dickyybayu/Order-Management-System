using System.ComponentModel.DataAnnotations;

namespace OMS.API.Domain.Category.Dtos;

public sealed class CreateCategoryRequest
{
    public CreateCategoryRequest(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    [Required]
    [MaxLength(100)]
    public string Name { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }
}
